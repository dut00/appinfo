using AspNetCore.AppInfo.Configuration.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AspNetCore.AppInfo.Configuration;

/// <summary>
/// Adds configuration details to the <c>/appinfo</c> response.
/// </summary>
public static class ConfigurationAppInfoBuilderExtensions
{
    /// <summary>
    /// Adds <c>ConfigurationsFiles</c>: the absolute paths of the file-based configuration sources
    /// (JSON, XML, INI, user secrets), in load order.
    /// </summary>
    /// <remarks>
    /// Safe to call more than once: the field is added once and every <paramref name="configure"/> delegate is applied.
    /// </remarks>
    /// <param name="builder">The AppInfo builder.</param>
    /// <param name="configure">Optional configuration of <see cref="ConfigurationDetailsOptions"/>.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IAppInfoBuilder WithConfigurationDetails(this IAppInfoBuilder builder, Action<ConfigurationDetailsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOptions<ConfigurationDetailsOptions>();
        if (configure is not null)
        {
            builder.Services.Configure(configure);
        }

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IAppInfoContributor, ConfigurationAppInfoContributor>());
        return builder;
    }
}
