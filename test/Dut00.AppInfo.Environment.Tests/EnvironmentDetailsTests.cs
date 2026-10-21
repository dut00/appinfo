using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Dut00.AppInfo.Environment.Internal;
using Dut00.AppInfo.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Dut00.AppInfo.Environment.Tests;

public sealed class EnvironmentDetailsTests
{
    // Tests control the uptime through the clock, relative to the real start time of the test process.
    private static readonly DateTimeOffset StartedAt = EnvironmentAppInfoContributor.ProcessStartTime!.Value;

    [Fact]
    public async Task WithEnvironmentDetails_AddsFourKeysAfterCoreFields()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo().WithEnvironmentDetails());

        var json = await app.GetAppInfoAsync();

        json.PropertyNames().ShouldBe(
        [
            "ApplicationName", "Version", "Environment", "IsProduction",
            "Process", "HostName", "ContentRootPath", "AssemblyLocation",
        ]);
    }

    [Fact]
    public async Task Process_HasIdStartTimeAndUptimeInThatOrder()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo().WithEnvironmentDetails());

        var json = await app.GetAppInfoAsync();

        json.GetProperty("Process").PropertyNames().ShouldBe(["Id", "StartTime", "Uptime"]);
    }

    [Fact]
    public async Task ProcessId_IsTheCurrentProcessId()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo().WithEnvironmentDetails());

        var json = await app.GetAppInfoAsync();

        json.GetProperty("Process").GetProperty("Id").GetInt32().ShouldBe(System.Environment.ProcessId);
    }

    [Fact]
    public void CreateProcessDetails_FormatsStartTimeInRoundTripUtcFormat()
    {
        var startTime = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(2));

        var details = EnvironmentAppInfoContributor.CreateProcessDetails(startTime, startTime + TimeSpan.FromMinutes(5));

        // Converted to UTC, with all seven fraction digits even when they are zero.
        details.StartTime.ShouldBe("2026-01-01T08:00:00.0000000Z");
        details.Uptime.ShouldBe("00:05:00");
        details.Id.ShouldBe(System.Environment.ProcessId);
    }

    [Fact]
    public void CreateProcessDetails_WithoutStartTime_LeavesStartTimeAndUptimeNull()
    {
        var details = EnvironmentAppInfoContributor.CreateProcessDetails(null, DateTimeOffset.UtcNow);

        details.StartTime.ShouldBeNull();
        details.Uptime.ShouldBeNull();
        details.Id.ShouldBe(System.Environment.ProcessId);
    }

    [Fact]
    public async Task ProcessStartTime_WithRealProcess_IsTheStartTimeInUtc()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo().WithEnvironmentDetails());

        var json = await app.GetAppInfoAsync();

        var value = json.GetProperty("Process").GetProperty("StartTime").GetString();
        value.ShouldNotBeNull();
        value.ShouldEndWith("Z");
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ShouldBe(StartedAt);
    }

    [Fact]
    public async Task HostName_IsTheMachineName()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo().WithEnvironmentDetails());

        var json = await app.GetAppInfoAsync();

        json.GetProperty("HostName").GetString().ShouldBe(System.Environment.MachineName);
    }

    [Fact]
    public async Task ContentRootPath_IsTheHostContentRoot()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo().WithEnvironmentDetails());

        var json = await app.GetAppInfoAsync();

        // TestApp sets the content root to the test output directory.
        Path.TrimEndingDirectorySeparator(json.GetProperty("ContentRootPath").GetString()!)
            .ShouldBe(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
    }

    [Fact]
    public async Task AssemblyLocation_IsTheEntryAssemblyFile()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo().WithEnvironmentDetails());

        var json = await app.GetAppInfoAsync();

        var location = json.GetProperty("AssemblyLocation").GetString();
        location.ShouldBe(Assembly.GetEntryAssembly()!.Location);
        Path.IsPathRooted(location).ShouldBeTrue();
        File.Exists(location).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("/opt/app-root/App.dll", "/opt/app-root/App.dll")]
    public void NormalizeLocation_EmptyLocationOfSingleFileAppBecomesNull(string? location, string? expected)
    {
        EnvironmentAppInfoContributor.NormalizeLocation(location).ShouldBe(expected);
    }

    [Fact]
    public async Task Uptime_UsesRegisteredTimeProviderAndIsComputedOnEveryRequest()
    {
        var time = new FakeTimeProvider(StartedAt + new TimeSpan(1, 2, 3, 4, 500));
        await using var app = await TestApp.StartAsync(b =>
        {
            b.Services.AddSingleton<TimeProvider>(time);
            b.Services.AddAppInfo().WithEnvironmentDetails();
        });

        (await app.GetAppInfoAsync()).GetProperty("Process").GetProperty("Uptime").GetString().ShouldBe("1.02:03:04.5000000");

        time.Advance(TimeSpan.FromHours(1));

        (await app.GetAppInfoAsync()).GetProperty("Process").GetProperty("Uptime").GetString().ShouldBe("1.03:03:04.5000000");
    }

    [Fact]
    public async Task Uptime_WithRealClock_IsAPositiveTimeSpanShorterThanTheProcessLifetime()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo().WithEnvironmentDetails());

        var json = await app.GetAppInfoAsync();

        var uptime = TimeSpan.ParseExact(json.GetProperty("Process").GetProperty("Uptime").GetString()!, "c", CultureInfo.InvariantCulture);
        uptime.ShouldBeGreaterThan(TimeSpan.Zero);
        uptime.ShouldBeLessThan(DateTimeOffset.UtcNow - StartedAt + TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(0, "00:00:00")]
    [InlineData(5 * TimeSpan.TicksPerSecond, "00:00:05")]
    [InlineData(TimeSpan.TicksPerDay + TimeSpan.TicksPerHour + 1, "1.01:00:00.0000001")]
    [InlineData(-TimeSpan.TicksPerSecond, "00:00:00")]
    public void FormatUptime_UsesConstantFormatAndClampsNegativeValues(long ticks, string expected)
    {
        EnvironmentAppInfoContributor.FormatUptime(TimeSpan.FromTicks(ticks)).ShouldBe(expected);
    }

    [Fact]
    public void ProcessStartTime_IsKnownInThePastAndInUtc()
    {
        var startedAt = EnvironmentAppInfoContributor.ProcessStartTime;

        startedAt.ShouldNotBeNull(); // Windows, Linux and macOS all expose it.
        startedAt.Value.Offset.ShouldBe(TimeSpan.Zero);
        startedAt.Value.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Uptime_UsesTimeProviderRegisteredAfterWithEnvironmentDetails()
    {
        var time = new FakeTimeProvider(StartedAt + TimeSpan.FromMinutes(5));
        await using var app = await TestApp.StartAsync(b =>
        {
            b.Services.AddAppInfo().WithEnvironmentDetails();
            b.Services.TryAddSingleton<TimeProvider>(time);
        });

        (await app.GetAppInfoAsync()).GetProperty("Process").GetProperty("Uptime").GetString().ShouldBe("00:05:00");
    }

    [Fact]
    public void WithEnvironmentDetails_CalledTwice_RegistersOnceAndAddsNoTimeProvider()
    {
        var services = new ServiceCollection();

        services.AddAppInfo()
            .WithEnvironmentDetails()
            .WithEnvironmentDetails();

        services.Count(d => d.ServiceType == typeof(IAppInfoContributor) && d.ImplementationType is null && d.ImplementationFactory is not null)
            .ShouldBe(1);
        services.ShouldNotContain(d => d.ServiceType == typeof(TimeProvider));
    }

    [Fact]
    public void WithEnvironmentDetails_NullBuilder_Throws()
    {
        Should.Throw<ArgumentNullException>(() => ((IAppInfoBuilder)null!).WithEnvironmentDetails());
    }

    [Fact]
    public async Task KeyNamingPolicy_AppliesToEnvironmentKeys()
    {
        await using var app = await TestApp.StartAsync(b => b.Services
            .AddAppInfo(o => o.KeyNamingPolicy = JsonNamingPolicy.CamelCase)
            .WithEnvironmentDetails());

        var json = await app.GetAppInfoAsync();

        json.PropertyNames().ShouldContain("hostName");
        json.GetProperty("process").PropertyNames().ShouldBe(["id", "startTime", "uptime"]);
    }
}
