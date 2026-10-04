using System.Globalization;
using System.Reflection;
using System.Text.Json;
using AspNetCore.AppInfo.Environment.Internal;
using AspNetCore.AppInfo.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace AspNetCore.AppInfo.Environment.Tests;

public sealed class EnvironmentDetailsTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task WithEnvironmentDetails_AddsFourKeysAfterCoreFields()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo().WithEnvironmentDetails());

        var json = await app.GetAppInfoAsync();

        json.PropertyNames().ShouldBe(
        [
            "ApplicationName", "Version", "Environment", "IsProduction",
            "ApplicationProcessUptime", "HostName", "ContentRootPath", "AssemblyLocation",
        ]);
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
            b.Services.AddSingleton(new ProcessStartTime(StartedAt));
            b.Services.AddAppInfo().WithEnvironmentDetails();
        });

        (await app.GetAppInfoAsync()).GetProperty("ApplicationProcessUptime").GetString().ShouldBe("1.02:03:04.5000000");

        time.Advance(TimeSpan.FromHours(1));

        (await app.GetAppInfoAsync()).GetProperty("ApplicationProcessUptime").GetString().ShouldBe("1.03:03:04.5000000");
    }

    [Fact]
    public async Task Uptime_WithRealClock_IsAPositiveTimeSpanShorterThanTheProcessLifetime()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo().WithEnvironmentDetails());

        var json = await app.GetAppInfoAsync();

        var uptime = TimeSpan.ParseExact(json.GetProperty("ApplicationProcessUptime").GetString()!, "c", CultureInfo.InvariantCulture);
        uptime.ShouldBeGreaterThan(TimeSpan.Zero);
        uptime.ShouldBeLessThan(DateTimeOffset.UtcNow - ProcessStartTime.Current.Value + TimeSpan.FromSeconds(1));
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
    public void ProcessStartTime_Current_IsInThePastAndInUtc()
    {
        var startedAt = ProcessStartTime.Current.Value;

        startedAt.Offset.ShouldBe(TimeSpan.Zero);
        startedAt.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Uptime_UsesTimeProviderRegisteredAfterWithEnvironmentDetails()
    {
        var time = new FakeTimeProvider(StartedAt + TimeSpan.FromMinutes(5));
        await using var app = await TestApp.StartAsync(b =>
        {
            b.Services.AddAppInfo().WithEnvironmentDetails();
            b.Services.TryAddSingleton<TimeProvider>(time);
            b.Services.AddSingleton(new ProcessStartTime(StartedAt));
        });

        (await app.GetAppInfoAsync()).GetProperty("ApplicationProcessUptime").GetString().ShouldBe("00:05:00");
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
        services.ShouldNotContain(d => d.ServiceType == typeof(ProcessStartTime));
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

        json.PropertyNames().ShouldContain("applicationProcessUptime");
        json.PropertyNames().ShouldContain("hostName");
    }
}
