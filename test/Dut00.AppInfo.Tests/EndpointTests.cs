using System.Net;
using Dut00.AppInfo.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Dut00.AppInfo.Tests;

public sealed class EndpointTests
{
    [Fact]
    public async Task MapAppInfo_DefaultPattern_ReturnsJsonOnAppInfo()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo());

        using var response = await app.GetAsync("/appinfo");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType.ShouldNotBeNull();
        response.Content.Headers.ContentType.MediaType.ShouldBe("application/json");
        response.Content.Headers.ContentType.CharSet.ShouldBe("utf-8");
    }

    [Fact]
    public async Task MapAppInfo_ResponseIsNotStored()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo());

        using var response = await app.GetAsync();

        response.Headers.CacheControl.ShouldNotBeNull();
        response.Headers.CacheControl.NoStore.ShouldBeTrue();
        response.Headers.CacheControl.ToString().ShouldBe("no-store");
    }

    [Fact]
    public async Task MapAppInfo_ContributorCanChangeCacheControl()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo().WithContributor<CacheControlContributor>());

        using var response = await app.GetAsync();

        response.Headers.CacheControl.ShouldNotBeNull();
        response.Headers.CacheControl.NoStore.ShouldBeFalse();
        response.Headers.CacheControl.MaxAge.ShouldBe(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public async Task MapAppInfo_CustomPattern_ServesOnlyThatPath()
    {
        await using var app = await TestApp.StartAsync(
            b => b.Services.AddAppInfo(),
            a => a.MapAppInfo("/custom-appinfo-path"));

        using var custom = await app.GetAsync("/custom-appinfo-path");
        using var defaultPath = await app.GetAsync("/appinfo");

        custom.StatusCode.ShouldBe(HttpStatusCode.OK);
        defaultPath.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MapAppInfo_CalledTwice_ServesBothPaths()
    {
        await using var app = await TestApp.StartAsync(
            b => b.Services.AddAppInfo(),
            a =>
            {
                a.MapAppInfo();
                a.MapAppInfo("/second");
            });

        using var first = await app.GetAsync("/appinfo");
        using var second = await app.GetAsync("/second");

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MapAppInfo_OnlyAnswersGet()
    {
        await using var app = await TestApp.StartAsync(b => b.Services.AddAppInfo());

        using var response = await app.Client.PostAsync("/appinfo", content: null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task MapAppInfo_WithoutAddAppInfo_Throws()
    {
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => TestApp.StartAsync(_ => { }, a => a.MapAppInfo()));

        exception.Message.ShouldContain("AddAppInfo()");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task MapAppInfo_InvalidPattern_Throws(string? pattern)
    {
        await Should.ThrowAsync<ArgumentException>(
            () => TestApp.StartAsync(b => b.Services.AddAppInfo(), a => a.MapAppInfo(pattern!)));
    }

    [Fact]
    public void MapAppInfo_NullEndpoints_Throws()
    {
        Should.Throw<ArgumentNullException>(() => ((IEndpointRouteBuilder)null!).MapAppInfo());
    }

    [Fact]
    public async Task MapAppInfo_SetsDisplayNameAndNoEndpointName()
    {
        RouteEndpoint? endpoint = null;
        await using var app = await TestApp.StartAsync(
            b => b.Services.AddAppInfo(),
            a =>
            {
                a.MapAppInfo();
                endpoint = ((IEndpointRouteBuilder)a).DataSources
                    .SelectMany(source => source.Endpoints)
                    .OfType<RouteEndpoint>()
                    .Single();
            });

        endpoint.ShouldNotBeNull();
        endpoint.DisplayName.ShouldBe("AppInfo");
        endpoint.Metadata.GetMetadata<IEndpointNameMetadata>().ShouldBeNull();
    }

    [Fact]
    public async Task MapAppInfo_ReturnsConventionBuilder_RequireAuthorizationApplies()
    {
        await using var app = await TestApp.StartAsync(
            b =>
            {
                b.Services.AddAuthentication(NoResultAuthenticationHandler.SchemeName)
                    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, NoResultAuthenticationHandler>(
                        NoResultAuthenticationHandler.SchemeName, configureOptions: null);
                b.Services.AddAuthorization();
                b.Services.AddAppInfo();
            },
            a => a.MapAppInfo().RequireAuthorization());

        using var response = await app.GetAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private sealed class CacheControlContributor : IAppInfoContributor
    {
        public ValueTask ContributeAsync(AppInfoContext context, CancellationToken cancellationToken)
        {
            context.HttpContext.Response.Headers.CacheControl = "private, max-age=60";
            return ValueTask.CompletedTask;
        }
    }
}
