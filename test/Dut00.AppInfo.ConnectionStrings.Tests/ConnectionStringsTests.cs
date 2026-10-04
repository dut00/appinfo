using System.Text.Json;
using Dut00.AppInfo.ConnectionStrings.Internal;
using Dut00.AppInfo.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dut00.AppInfo.ConnectionStrings.Tests;

public sealed class ConnectionStringsTests
{
    [Fact]
    public async Task ConnectionStrings_AreWrittenByNameWithSecretsMasked()
    {
        await using var app = await TestApp.StartAsync(b =>
        {
            b.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Billing"] = "Data Source=db.company.com;Initial Catalog=Billing;User ID=sa;Password=S3cr3t",
                ["ConnectionStrings:Cache"] = "cache:6379,password=S3cr3t",
            });
            b.Services.AddAppInfo().WithConnectionStrings();
        });

        var body = await app.GetAppInfoStringAsync();
        var connectionStrings = JsonDocument.Parse(body).RootElement.GetProperty("ConnectionStrings");

        connectionStrings.PropertyNames().ShouldBe(["Billing", "Cache"]);
        connectionStrings.GetProperty("Billing").GetString()
            .ShouldBe("Data Source=db.company.com;Initial Catalog=Billing;User ID=***;Password=***");
        connectionStrings.GetProperty("Cache").GetString().ShouldBe("***");
        body.ShouldNotContain("S3cr3t");
    }

    [Fact]
    public async Task ConnectionStrings_ComesAfterCoreFieldsAndIsEmptyWithoutSection()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo().WithConnectionStrings());

        var json = await app.GetAppInfoAsync();

        json.PropertyNames().ShouldBe(["ApplicationName", "Version", "Environment", "IsProduction", "ConnectionStrings"]);
        json.GetProperty("ConnectionStrings").ValueKind.ShouldBe(JsonValueKind.Object);
        json.GetProperty("ConnectionStrings").PropertyNames().ShouldBeEmpty();
    }

    [Fact]
    public async Task NestedSectionUnderConnectionStrings_IsLeftOut()
    {
        await using var app = await TestApp.StartAsync(b =>
        {
            b.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Main"] = "Host=db",
                ["ConnectionStrings:Nested:Password"] = "S3cr3t",
            });
            b.Services.AddAppInfo().WithConnectionStrings();
        });

        var body = await app.GetAppInfoStringAsync();

        JsonDocument.Parse(body).RootElement.GetProperty("ConnectionStrings").PropertyNames().ShouldBe(["Main"]);
        body.ShouldNotContain("S3cr3t");
    }

    [Fact]
    public async Task Options_CustomKeysAndMaskAreApplied()
    {
        await using var app = await TestApp.StartAsync(b =>
        {
            b.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Main"] = "Host=db;Tenant=contoso;Password=S3cr3t",
            });
            b.Services.AddAppInfo().WithConnectionStrings(o =>
            {
                o.SensitiveKeys.Add("Tenant");
                o.Mask = "<hidden>";
            });
        });

        var json = await app.GetAppInfoAsync();

        json.GetProperty("ConnectionStrings").GetProperty("Main").GetString().ShouldBe("Host=db;Tenant=<hidden>;Password=<hidden>");
    }

    [Fact]
    public async Task KeyNamingPolicy_ConvertsFieldNameButNotConnectionStringNames()
    {
        await using var app = await TestApp.StartAsync(b =>
        {
            b.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:BillingDb"] = "Host=db" });
            b.Services.AddAppInfo(o => o.KeyNamingPolicy = JsonNamingPolicy.CamelCase).WithConnectionStrings();
        });

        var json = await app.GetAppInfoAsync();

        json.GetProperty("connectionStrings").PropertyNames().ShouldBe(["BillingDb"]);
    }

    [Fact]
    public void WithConnectionStrings_CalledTwice_RegistersOnceAndAppliesEveryConfigure()
    {
        var services = new ServiceCollection();

        services.AddAppInfo()
            .WithConnectionStrings(o => o.SensitiveKeys.Add("Tenant"))
            .WithConnectionStrings(o => o.Mask = "#");

        services.Count(d => d.ServiceType == typeof(IAppInfoContributor) && d.ImplementationType == typeof(ConnectionStringsAppInfoContributor))
            .ShouldBe(1);
        var options = services.BuildServiceProvider().GetRequiredService<IOptions<ConnectionStringsOptions>>().Value;
        options.SensitiveKeys.ShouldContain("tenant");
        options.Mask.ShouldBe("#");
    }

    [Fact]
    public void Options_DefaultsMatchTheSpec()
    {
        var options = new ConnectionStringsOptions();

        options.Mask.ShouldBe("***");
        options.SensitiveKeys.ShouldBe(
            ["Password", "Pwd", "PSW", "Pass", "User ID", "UID", "User", "Username", "Key", "AccountKey", "SharedAccessKey",
             "SharedAccessSignature", "AccessKey", "ApiKey", "Secret", "Token", "Credential", "Authorization", "Signature", "Bearer"],
            ignoreOrder: true);
        options.SensitiveKeys.Contains("PASSWORD").ShouldBeTrue();
        Should.Throw<ArgumentNullException>(() => options.Mask = null!);
    }

    [Fact]
    public void WithConnectionStrings_NullBuilder_Throws()
    {
        Should.Throw<ArgumentNullException>(() => ((IAppInfoBuilder)null!).WithConnectionStrings());
    }
}
