using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;

namespace AspNetCore.AppInfo.Tests.Infrastructure;

/// <summary>
/// A minimal web application running on <see cref="TestServer"/>.
/// </summary>
internal sealed class TestApp : IAsyncDisposable
{
    public const string DefaultEnvironmentName = "Testing";
    public const string ApplicationName = "AppInfo.TestHost";

    private readonly WebApplication _app;

    private TestApp(WebApplication app, ListLoggerProvider logs)
    {
        _app = app;
        Logs = logs;
        Client = app.GetTestClient();
    }

    public HttpClient Client { get; }

    public ListLoggerProvider Logs { get; }

    /// <summary>
    /// Builds and starts the application.
    /// </summary>
    /// <param name="configure">Registers services, usually <c>AddAppInfo()</c>.</param>
    /// <param name="configureApp">Maps endpoints. Defaults to <c>app.MapAppInfo()</c>.</param>
    /// <param name="environmentName">The hosting environment name.</param>
    public static async Task<TestApp> StartAsync(
        Action<WebApplicationBuilder> configure,
        Action<WebApplication>? configureApp = null,
        string environmentName = DefaultEnvironmentName)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = ApplicationName,
            EnvironmentName = environmentName,
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.WebHost.UseTestServer();

        var logs = new ListLoggerProvider();
        builder.Logging.ClearProviders().AddProvider(logs);

        configure(builder);

        var app = builder.Build();
        try
        {
            (configureApp ?? (a => a.MapAppInfo())).Invoke(app);
            await app.StartAsync(TestContext.Current.CancellationToken);
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }

        return new TestApp(app, logs);
    }

    public Task<HttpResponseMessage> GetAsync(string path = AppInfoEndpointRouteBuilderExtensions.DefaultPattern) =>
        Client.GetAsync(path, TestContext.Current.CancellationToken);

    public async Task<string> GetAppInfoStringAsync(string path = AppInfoEndpointRouteBuilderExtensions.DefaultPattern)
    {
        using var response = await GetAsync(path);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
    }

    public async Task<JsonElement> GetAppInfoAsync(string path = AppInfoEndpointRouteBuilderExtensions.DefaultPattern)
    {
        using var document = JsonDocument.Parse(await GetAppInfoStringAsync(path));
        return document.RootElement.Clone();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }
}
