using Dut00.AppInfo.Server.Internal;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dut00.AppInfo.Server;

/// <summary>
/// Adds the server's listening addresses to the <c>/appinfo</c> response.
/// </summary>
public static class ServerAppInfoBuilderExtensions
{
    /// <summary>
    /// Adds <c>Urls</c>: the addresses the server listens on, for example <c>http://[::]:8080</c>.
    /// </summary>
    /// <remarks>
    /// The addresses come from the server's <c>IServerAddressesFeature</c> and are read on every request, when the
    /// server has started and they are final. They are written as the server reports them, wildcards included.
    /// <c>Urls</c> is <see langword="null"/> when no server is registered or the server doesn't expose its addresses.
    /// Safe to call more than once.
    /// </remarks>
    /// <param name="builder">The AppInfo builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IAppInfoBuilder WithServerAddresses(this IAppInfoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IAppInfoContributor, ServerAppInfoContributor>(
            services => new ServerAppInfoContributor(services.GetService<IServer>())));
        return builder;
    }
}
