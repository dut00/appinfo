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
    /// Adds <c>Process</c> (<c>Id</c>, <c>StartTime</c>, <c>Uptime</c>), <c>HostName</c>, <c>ContentRootPath</c> and
    /// <c>AssemblyLocation</c>.
    /// </summary>
    /// <remarks>
    /// <c>Process.Uptime</c> is computed on every request with the application's <see cref="TimeProvider"/> if one is registered,
    /// otherwise with <see cref="TimeProvider.System"/>. This method registers no <see cref="TimeProvider"/> itself.
    /// <c>Process.StartTime</c> is a UTC timestamp in the round-trip format. When the platform doesn't expose the start time,
    /// both <c>Process.StartTime</c> and <c>Process.Uptime</c> are <see langword="null"/>.
    /// Safe to call more than once.
    /// </remarks>
    /// <param name="builder">The AppInfo builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IAppInfoBuilder WithEnvironmentDetails(this IAppInfoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // ASP.NET Core registers no TimeProvider by default, and this package doesn't add one to the app's services.
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IAppInfoContributor, EnvironmentAppInfoContributor>(
            services => new EnvironmentAppInfoContributor(
                services.GetRequiredService<IHostEnvironment>(),
                services.GetService<TimeProvider>() ?? TimeProvider.System)));
        return builder;
    }
}
