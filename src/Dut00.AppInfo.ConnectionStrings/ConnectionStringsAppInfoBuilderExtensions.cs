using Dut00.AppInfo.ConnectionStrings.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dut00.AppInfo.ConnectionStrings;

/// <summary>
/// Adds masked connection strings to the <c>/appinfo</c> response.
/// </summary>
public static class ConnectionStringsAppInfoBuilderExtensions
{
    /// <summary>
    /// Adds <c>ConnectionStrings</c>: every value of the <c>ConnectionStrings</c> configuration section, by name,
    /// with secrets masked.
    /// </summary>
    /// <remarks>
    /// Masking fails closed: a value that can't be parsed as <c>key=value</c> pairs, or that looks like a URI,
    /// is masked completely. Safe to call more than once: the field is added once and every
    /// <paramref name="configure"/> delegate is applied.
    /// </remarks>
    /// <param name="builder">The AppInfo builder.</param>
    /// <param name="configure">Optional configuration of <see cref="ConnectionStringsOptions"/>.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IAppInfoBuilder WithConnectionStrings(this IAppInfoBuilder builder, Action<ConnectionStringsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOptions<ConnectionStringsOptions>();
        if (configure is not null)
        {
            builder.Services.Configure(configure);
        }

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IAppInfoContributor, ConnectionStringsAppInfoContributor>());
        return builder;
    }
}
