using System.Text.Json;
using AspNetCore.AppInfo.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AspNetCore.AppInfo.Tests;

public sealed class NamingPolicyTests
{
    [Fact]
    public async Task CamelCase_ConvertsRootKeysAndNestedPropertiesButNotDictionaryKeys()
    {
        await using var app = await TestApp.StartAsync(b => b.Services
            .AddAppInfo(o => o.KeyNamingPolicy = JsonNamingPolicy.CamelCase)
            .WithOwner(o => o.Owner = "owner")
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
    public void KeyNamingPolicy_SetsPropertyNamingPolicyOnly()
    {
        var options = new AppInfoOptions { KeyNamingPolicy = JsonNamingPolicy.CamelCase };

        options.KeyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
        options.JsonSerializerOptions.PropertyNamingPolicy.ShouldBe(JsonNamingPolicy.CamelCase);
        options.JsonSerializerOptions.DictionaryKeyPolicy.ShouldBeNull();
    }
}
