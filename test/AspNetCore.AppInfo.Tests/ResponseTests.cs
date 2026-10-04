using System.Net;
using System.Reflection;
using System.Text.Json;
using AspNetCore.AppInfo.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AspNetCore.AppInfo.Tests;

public sealed class ResponseTests
{
    [Fact]
    public async Task CoreFields_AreWrittenFromHostEnvironmentAndEntryAssembly()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo());

        var json = await app.GetAppInfoAsync();

        json.PropertyNames().ShouldBe(["ApplicationName", "Version", "Environment", "IsProduction"]);
        json.GetProperty("ApplicationName").GetString().ShouldBe(TestApp.ApplicationName);

        // The test assembly is the entry assembly; its version comes from <Version> in Directory.Build.props.
        Assembly.GetEntryAssembly().ShouldBe(typeof(ResponseTests).Assembly);
        json.GetProperty("Version").GetString().ShouldBe("0.1.0.0");
        json.GetProperty("Environment").GetString().ShouldBe(TestApp.DefaultEnvironmentName);
        json.GetProperty("IsProduction").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task IsProduction_InProductionEnvironment_IsTrue()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo(), environmentName: "Production");

        var json = await app.GetAppInfoAsync();

        json.GetProperty("Environment").GetString().ShouldBe("Production");
        json.GetProperty("IsProduction").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Response_IsIndentedByDefault()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo());

        var body = await app.GetAppInfoStringAsync();

        body.ShouldContain("\n  \"ApplicationName\"");
    }

    [Fact]
    public async Task Owner_IsLeftOutUnlessWithOwnerIsCalled()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo());

        var json = await app.GetAppInfoAsync();

        json.TryGetProperty("Owner", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task WithOwner_WritesOwner()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo().WithOwner(o => o.Owner = "red_team@company.com"));

        var json = await app.GetAppInfoAsync();

        json.GetProperty("Owner").GetString().ShouldBe("red_team@company.com");
    }

    [Fact]
    public async Task WithOwner_WithoutValue_WritesNull()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo().WithOwner(_ => { }));

        var json = await app.GetAppInfoAsync();

        json.GetProperty("Owner").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task WithProperty_ConstantValues_AreSerialized()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo()
            .WithProperty("Team", "red")
            .WithProperty("Replicas", 3)
            .WithProperty("Tags", new[] { "a", "b" })
            .WithProperty("Details", new { Region = "eu", Zone = 2 })
            .WithProperty("Nothing", (object?)null));

        var json = await app.GetAppInfoAsync();

        json.GetProperty("Team").GetString().ShouldBe("red");
        json.GetProperty("Replicas").GetInt32().ShouldBe(3);
        json.GetProperty("Tags").EnumerateArray().Select(e => e.GetString()).ShouldBe(["a", "b"]);
        json.GetProperty("Details").GetProperty("Region").GetString().ShouldBe("eu");
        json.GetProperty("Details").GetProperty("Zone").GetInt32().ShouldBe(2);
        json.GetProperty("Nothing").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task WithProperty_Factory_IsEvaluatedOnEveryRequest()
    {
        var calls = 0;
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo()
            .WithProperty("Calls", _ => Interlocked.Increment(ref calls)));

        (await app.GetAppInfoAsync()).GetProperty("Calls").GetInt32().ShouldBe(1);
        (await app.GetAppInfoAsync()).GetProperty("Calls").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task WithProperty_FactoryAndContributor_ShareTheRequestScope()
    {
        await using var app = await TestApp.StartAsync(b =>
        {
            b.Services.AddScoped<RequestMarker>();
            b.Services.AddAppInfo()
                .WithProperty("FromFactory", sp => sp.GetRequiredService<RequestMarker>().Id)
                .WithContributor<RequestMarkerContributor>();
        });

        var first = await app.GetAppInfoAsync();
        var second = await app.GetAppInfoAsync();

        first.GetProperty("FromFactory").GetGuid().ShouldBe(first.GetProperty("FromContributor").GetGuid());
        second.GetProperty("FromFactory").GetGuid().ShouldNotBe(first.GetProperty("FromFactory").GetGuid());
    }

    [Fact]
    public async Task Keys_FollowRegistrationOrder()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo()
            .WithProperty("Zeta", 1)
            .WithOwner(o => o.Owner = "owner")
            .WithContributor<TwoKeysContributor>()
            .WithProperty("Alpha", 2));

        var json = await app.GetAppInfoAsync();

        json.PropertyNames().ShouldBe(
            ["ApplicationName", "Version", "Environment", "IsProduction", "Zeta", "Owner", "Second", "First", "Alpha"]);
    }

    [Fact]
    public async Task DuplicateKey_LastValueWinsAtFirstPositionAndLogsWarning()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo()
            .WithProperty("Team", "red")
            .WithProperty("Other", 1)
            .WithProperty("Team", "blue"));

        var json = await app.GetAppInfoAsync();

        json.PropertyNames().ShouldBe(["ApplicationName", "Version", "Environment", "IsProduction", "Team", "Other"]);
        json.GetProperty("Team").GetString().ShouldBe("blue");
        AppInfoWarnings(app).ShouldHaveSingleItem().Message.ShouldContain("'Team'");
    }

    [Fact]
    public async Task KeysCollidingAfterNamingPolicy_LastValueWinsAndLogsWarning()
    {
        await using var app = await TestApp.StartAsync(b => b.Services
            .AddAppInfo(o => o.KeyNamingPolicy = JsonNamingPolicy.CamelCase)
            .WithProperty("Team", "red")
            .WithProperty("team", "blue"));

        var json = await app.GetAppInfoAsync();

        json.PropertyNames().Count(name => name == "team").ShouldBe(1);
        json.GetProperty("team").GetString().ShouldBe("blue");
        AppInfoWarnings(app).ShouldHaveSingleItem().Message.ShouldContain("'team'");
    }

    [Fact]
    public async Task Contributor_ReceivesRequestContextAndAbortToken()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo().WithContributor<RequestDetailsContributor>());

        var json = await app.GetAppInfoAsync();

        json.GetProperty("Path").GetString().ShouldBe("/appinfo");
        json.GetProperty("TokenIsRequestAborted").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Contributor_Throwing_PropagatesTheException()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo().WithContributor<ThrowingContributor>());

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => app.GetAsync());

        exception.Message.ShouldBe(ThrowingContributor.Message);
    }

    [Fact]
    public async Task Contributor_Throwing_ReachesAppErrorHandlingWhichReturns500()
    {
        Exception? caught = null;
        await using var app = await TestApp.StartAsync(
            b => b.Services.AddAppInfo().WithContributor<ThrowingContributor>(),
            a =>
            {
                a.Use(async (httpContext, next) =>
                {
                    try
                    {
                        await next(httpContext);
                    }
                    catch (Exception exception)
                    {
                        caught = exception;
                        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    }
                });
                a.MapAppInfo();
            });

        using var response = await app.GetAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        caught.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe(ThrowingContributor.Message);
    }

    private static IEnumerable<LogEntry> AppInfoWarnings(TestApp app) =>
        app.Logs.Entries.Where(e => e.Category == "AspNetCore.AppInfo" && e.Level == LogLevel.Warning);

    private sealed class RequestMarker
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    private sealed class RequestMarkerContributor(RequestMarker marker) : IAppInfoContributor
    {
        public ValueTask ContributeAsync(AppInfoContext context, CancellationToken cancellationToken)
        {
            context.Set("FromContributor", marker.Id);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TwoKeysContributor : IAppInfoContributor
    {
        public ValueTask ContributeAsync(AppInfoContext context, CancellationToken cancellationToken)
        {
            context.Set("Second", 2);
            context.Set("First", 1);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RequestDetailsContributor : IAppInfoContributor
    {
        public ValueTask ContributeAsync(AppInfoContext context, CancellationToken cancellationToken)
        {
            context.Set("Path", context.HttpContext.Request.Path.Value);
            context.Set("TokenIsRequestAborted", cancellationToken == context.HttpContext.RequestAborted);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingContributor : IAppInfoContributor
    {
        public const string Message = "Contributor failed.";

        public ValueTask ContributeAsync(AppInfoContext context, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(Message);
    }
}
