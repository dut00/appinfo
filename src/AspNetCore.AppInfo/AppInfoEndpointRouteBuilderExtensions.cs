using System.Text.Json;
using System.Text.Json.Nodes;
using AspNetCore.AppInfo.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AspNetCore.AppInfo;

/// <summary>
/// Maps the AppInfo endpoint.
/// </summary>
public static class AppInfoEndpointRouteBuilderExtensions
{
    /// <summary>
    /// The path used when none is given to <see cref="MapAppInfo"/>.
    /// </summary>
    public const string DefaultPattern = "/appinfo";

    internal const string EndpointDisplayName = "AppInfo";
    internal const string LoggerCategory = "AspNetCore.AppInfo";

    /// <summary>
    /// Maps a <c>GET</c> endpoint that returns the AppInfo JSON document.
    /// </summary>
    /// <remarks>
    /// The endpoint is open by default. Protect it with conventions on the returned builder,
    /// for example <c>.RequireAuthorization()</c> or <c>.RequireHost()</c>.
    /// </remarks>
    /// <param name="endpoints">The application's endpoint route builder.</param>
    /// <param name="pattern">The route pattern. Defaults to <c>/appinfo</c>.</param>
    /// <returns>A builder for applying endpoint conventions.</returns>
    /// <exception cref="InvalidOperationException"><c>AddAppInfo()</c> was not called.</exception>
    public static IEndpointConventionBuilder MapAppInfo(this IEndpointRouteBuilder endpoints, string pattern = DefaultPattern)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        if (endpoints.ServiceProvider.GetService<AppInfoMarkerService>() is null)
        {
            throw new InvalidOperationException(
                "Unable to find the required AppInfo services. " +
                "Call 'builder.Services.AddAppInfo()' before calling 'MapAppInfo()'.");
        }

        return endpoints
            .MapGet(pattern, WriteAppInfoAsync)
            .WithDisplayName(EndpointDisplayName);
    }

    private static async Task WriteAppInfoAsync(HttpContext httpContext)
    {
        var services = httpContext.RequestServices;
        var options = services.GetRequiredService<IOptions<AppInfoOptions>>().Value;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerCategory);
        var cancellationToken = httpContext.RequestAborted;

        var context = new AppInfoContext(httpContext, logger);
        foreach (var contributor in services.GetServices<IAppInfoContributor>())
        {
            await contributor.ContributeAsync(context, cancellationToken);
        }

        var document = ToJson(context, options, logger);
        await httpContext.Response.WriteAsJsonAsync(document, options.JsonSerializerOptions, cancellationToken);
    }

    // Built as a JsonObject so keys keep the order in which contributors set them.
    private static JsonObject ToJson(AppInfoContext context, AppInfoOptions options, ILogger logger)
    {
        var keyPolicy = options.KeyNamingPolicy;
        var serializerOptions = options.JsonSerializerOptions;
        var document = new JsonObject();
        foreach (var (key, value) in context.Entries)
        {
            var name = keyPolicy?.ConvertName(key) ?? key;
            if (document.ContainsKey(name))
            {
                // Distinct keys such as "Team" and "team" can collide once the naming policy is applied.
                logger.LogWarning(
                    "AppInfo key '{Key}' is written as '{Name}', which another key already uses. The last value wins.", key, name);
            }

            document[name] = value is null ? null : JsonSerializer.SerializeToNode(value, value.GetType(), serializerOptions);
        }

        return document;
    }
}
