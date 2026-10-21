using System.Text.Encodings.Web;
using Dut00.AppInfo.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Dut00.AppInfo.Tests;

public sealed class EscapingTests
{
    private const string Value = "1.0.5+a1b2c3d <dev> 'Zażółć' & \"quotes\" \\ C:\\app";

    [Fact]
    public async Task Default_WritesReadableCharactersAsIs()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo().WithProperty("Value", Value));

        var body = await app.GetAppInfoStringAsync();

        body.ShouldContain("""
            "Value": "1.0.5+a1b2c3d <dev> 'Zażółć' & \"quotes\" \\ C:\\app"
            """);
    }

    [Fact]
    public async Task Default_AppliesToRootKeysAndNestedValuesToo()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo()
            .WithProperty("Wersja+ł", new Dictionary<string, object> { ["a+b"] = "x<y", ["Nested"] = new { Text = "ż>z" } }));

        var body = await app.GetAppInfoStringAsync();

        body.ShouldContain("\"Wersja+ł\"");
        body.ShouldContain("\"a+b\": \"x<y\"");
        body.ShouldContain("\"Text\": \"ż>z\"");
    }

    [Fact]
    public async Task DefaultEncoder_RestoresStrictEscaping()
    {
        await using var app = await TestApp.StartAsync(b => b.Services
            .AddAppInfo(o => o.JsonSerializerOptions.Encoder = JavaScriptEncoder.Default)
            .WithProperty("Value", "1.0.5+a1b2c3d <dev>"));

        var body = await app.GetAppInfoStringAsync();

        body.ShouldContain("""
            "Value": "1.0.5\u002Ba1b2c3d \u003Cdev\u003E"
            """);
    }

    [Fact]
    public async Task Default_StillReturnsValidJson()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo().WithProperty("Value", Value));

        var json = await app.GetAppInfoAsync();

        json.GetProperty("Value").GetString().ShouldBe(Value);
    }
}
