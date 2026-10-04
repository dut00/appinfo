using Dut00.AppInfo.Environment.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Dut00.AppInfo.Environment;

/// <summary>
/// Adds process and host details to the <c>/appinfo</c> response.
/// </summary>
public static class EnvironmentAppInfoBuilderExtensions
{
    /// <summary>
    /// Adds <c>ApplicationProcessUptime</c>, <c>HostName</c>, <c>ContentRootPath</c> and <c>AssemblyLocation</c>.
    /// </summary>
    /// <remarks>
    /// The uptime is computed on every request with the application's <see cref="TimeProvider"/> if one is registered,
    /// otherwise with <see cref="TimeProvider.System"/>. This method registers no <see cref="TimeProvider"/> itself.
    /// Safe to call more than once.
    /// </remarks>
    /// <param name="builder">The AppInfo builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IAppInfoBuilder WithEnvironmentDetails(this IAppInfoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IAppInfoContributor, EnvironmentAppInfoContributor>(
            services => new EnvironmentAppInfoContributor(
                services.GetRequiredService<IHostEnvironment>(),
                services.GetService<TimeProvider>() ?? TimeProvider.System,
                services.GetService<ProcessStartTime>() ?? ProcessStartTime.Current)));
        return builder;
    }
}
