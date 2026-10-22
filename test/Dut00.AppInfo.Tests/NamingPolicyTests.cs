using System.Text.Json;
using Dut00.AppInfo.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dut00.AppInfo.Tests;

public sealed class NamingPolicyTests
{
    [Fact]
    public async Task CamelCase_ConvertsRootKeysAndNestedPropertiesButNotDictionaryKeys()
    {
        await using var app = await TestApp.StartAsync(b => b.Services
            .AddAppInfo(o => o.KeyNamingPolicy = JsonNamingPolicy.CamelCase)
            .WithOwner("owner")
            .WithProperty("Details", new { InnerValue = 1 })
            .WithProperty("Names", new Dictionary<string, string> { ["BillingDb"] = "x" }));

        var json = await app.GetAppInfoAsync();

        json.PropertyNames().ShouldBe(["applicationName", "version", "environment", "isProduction", "owner", "details", "names"]);
        json.GetProperty("details").PropertyNames().ShouldBe(["innerValue"]);
        json.GetProperty("names").PropertyNames().ShouldBe(["BillingDb"]);
    }

    [Fact]
    public async Task SnakeCaseLower_ConvertsRootKeys()
    {
        await using var app = await TestApp.StartAsync(b => b.Services
            .AddAppInfo(o => o.KeyNamingPolicy = JsonNamingPolicy.SnakeCaseLower));

        var json = await app.GetAppInfoAsync();

        json.PropertyNames().ShouldBe(["application_name", "version", "environment", "is_production"]);
    }

    [Fact]
    public async Task Default_EmitsKeysAsDefined()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo()
            .WithProperty("Details", new { InnerValue = 1 }));

        var json = await app.GetAppInfoAsync();

        json.GetProperty("Details").PropertyNames().ShouldBe(["InnerValue"]);
    }

    [Fact]
    public async Task SerializerPropertyNamingPolicy_AppliesToNestedValuesButNotRootKeys()
    {
        await using var app = await TestApp.StartAsync(b => b.Services
            .AddAppInfo(o => o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
            .WithProperty("Details", new { InnerValue = 1 }));

        var json = await app.GetAppInfoAsync();

        json.PropertyNames().ShouldBe(["ApplicationName", "Version", "Environment", "IsProduction", "Details"]);
        json.GetProperty("Details").PropertyNames().ShouldBe(["innerValue"]);
    }

    [Fact]
    public async Task SerializerDictionaryKeyPolicy_ConvertsNestedDictionaryKeysButNotRootKeys()
    {
        await using var app = await TestApp.StartAsync(b => b.Services
            .AddAppInfo(o => o.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase)
            .WithProperty("Names", new Dictionary<string, string> { ["BillingDb"] = "x" }));

        var json = await app.GetAppInfoAsync();

        json.PropertyNames().ShouldBe(["ApplicationName", "Version", "Environment", "IsProduction", "Names"]);
        json.GetProperty("Names").PropertyNames().ShouldBe(["billingDb"]);
    }

    [Fact]
    public async Task KeyNamingPolicy_WithReadOnlySerializerOptions_Works()
    {
        // JsonSerializerOptions.Default is read-only: the policy must not be set on it.
        await using var app = await TestApp.StartAsync(b => b.Services
            .AddAppInfo(o =>
            {
                o.JsonSerializerOptions = JsonSerializerOptions.Default;
                o.KeyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            })
            .WithProperty("Details", new { InnerValue = 1 }));

        var json = await app.GetAppInfoAsync();

        json.PropertyNames().ShouldBe(["application_name", "version", "environment", "is_production", "details"]);
        json.GetProperty("details").PropertyNames().ShouldBe(["inner_value"]);
        JsonSerializerOptions.Default.PropertyNamingPolicy.ShouldBeNull();
    }

    [Fact]
    public async Task KeyNamingPolicy_SetBeforeReplacingSerializerOptions_StillAppliesToNestedProperties()
    {
        var replacement = new JsonSerializerOptions();
        await using var app = await TestApp.StartAsync(b => b.Services
            .AddAppInfo(o =>
            {
                o.KeyNamingPolicy = JsonNamingPolicy.CamelCase;
                o.JsonSerializerOptions = replacement;
            })
            .WithProperty("Details", new { InnerValue = 1 }));

        var json = await app.GetAppInfoAsync();

        json.GetProperty("details").PropertyNames().ShouldBe(["innerValue"]);
        replacement.PropertyNamingPolicy.ShouldBeNull();
    }

    [Fact]
    public void KeyNamingPolicy_DoesNotChangeSerializerOptionsByItself()
    {
        var options = new AppInfoOptions { KeyNamingPolicy = JsonNamingPolicy.CamelCase };

        options.JsonSerializerOptions.PropertyNamingPolicy.ShouldBeNull();
    }
}
