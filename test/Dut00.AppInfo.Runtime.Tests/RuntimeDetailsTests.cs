using System.Runtime.InteropServices;
using System.Text.Json;
using Dut00.AppInfo.Runtime.Internal;
using Dut00.AppInfo.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dut00.AppInfo.Runtime.Tests;

public sealed class RuntimeDetailsTests
{
    [Fact]
    public async Task WithRuntimeDetails_AddsRuntimeAfterCoreFields()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo().WithRuntimeDetails());

        var json = await app.GetAppInfoAsync();

        json.PropertyNames().ShouldBe(["ApplicationName", "Version", "Environment", "IsProduction", "Runtime"]);
    }

    [Fact]
    public async Task Runtime_HasAllPropertiesInOrderWithValuesFromRuntimeInformation()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo().WithRuntimeDetails());

        var runtime = (await app.GetAppInfoAsync()).GetProperty("Runtime");

        runtime.PropertyNames().ShouldBe(["Framework", "RuntimeIdentifier", "OSDescription", "OSArchitecture", "ProcessArchitecture"]);
        runtime.GetProperty("Framework").GetString().ShouldBe(RuntimeInformation.FrameworkDescription);
        runtime.GetProperty("RuntimeIdentifier").GetString().ShouldBe(RuntimeInformation.RuntimeIdentifier);
        runtime.GetProperty("OSDescription").GetString().ShouldBe(RuntimeInformation.OSDescription);
        runtime.GetProperty("OSArchitecture").GetString().ShouldBe(RuntimeInformation.OSArchitecture.ToString());
        runtime.GetProperty("ProcessArchitecture").GetString().ShouldBe(RuntimeInformation.ProcessArchitecture.ToString());
    }

    [Fact]
    public async Task Architectures_AreEnumNamesEvenWithAStringEnumConverterThatChangesCasing()
    {
        await using var app = await TestApp.StartAsync(b => b.Services
            .AddAppInfo(o => o.JsonSerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)))
            .WithRuntimeDetails());

        var runtime = (await app.GetAppInfoAsync()).GetProperty("Runtime");

        runtime.GetProperty("OSArchitecture").GetString().ShouldBe(RuntimeInformation.OSArchitecture.ToString());
        runtime.GetProperty("ProcessArchitecture").GetString().ShouldBe(RuntimeInformation.ProcessArchitecture.ToString());
    }

    [Fact]
    public async Task KeyNamingPolicy_AppliesToNestedNames()
    {
        await using var app = await TestApp.StartAsync(b => b.Services
            .AddAppInfo(o => o.KeyNamingPolicy = JsonNamingPolicy.CamelCase)
            .WithRuntimeDetails());

        var json = await app.GetAppInfoAsync();

        json.GetProperty("runtime").PropertyNames()
            .ShouldBe(["framework", "runtimeIdentifier", "osDescription", "osArchitecture", "processArchitecture"]);
    }

    [Fact]
    public void WithRuntimeDetails_CalledTwice_RegistersOnce()
    {
        var services = new ServiceCollection();

        services.AddAppInfo()
            .WithRuntimeDetails()
            .WithRuntimeDetails();

        services.Count(d => d.ServiceType == typeof(IAppInfoContributor) && d.ImplementationType == typeof(RuntimeAppInfoContributor))
            .ShouldBe(1);
    }

    [Fact]
    public void WithRuntimeDetails_NullBuilder_Throws()
    {
        Should.Throw<ArgumentNullException>(() => ((IAppInfoBuilder)null!).WithRuntimeDetails());
    }
}
