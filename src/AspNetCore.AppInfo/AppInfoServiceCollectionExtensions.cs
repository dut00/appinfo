using AspNetCore.AppInfo.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AspNetCore.AppInfo;

/// <summary>
/// Registers AppInfo services.
/// </summary>
public static class AppInfoServiceCollectionExtensions
{
    /// <summary>
    /// Registers the services behind the <c>/appinfo</c> endpoint and its core fields:
    /// <c>ApplicationName</c>, <c>Version</c>, <c>Environment</c> and <c>IsProduction</c>.
    /// </summary>
    /// <remarks>
    /// Safe to call more than once: services are registered once and every <paramref name="configure"/> delegate is applied.
    /// </remarks>
    /// <param name="services">The application's service collection.</param>
    /// <param name="configure">Optional configuration of <see cref="AppInfoOptions"/>.</param>
    /// <returns>A builder for adding more fields.</returns>
    public static IAppInfoBuilder AddAppInfo(this IServiceCollection services, Action<AppInfoOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<AppInfoOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<AppInfoMarkerService>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAppInfoContributor, CoreAppInfoContributor>());

        return new AppInfoBuilder(services);
    }
}
