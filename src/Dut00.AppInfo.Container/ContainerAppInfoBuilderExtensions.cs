using Dut00.AppInfo.Container.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Dut00.AppInfo.Container;

/// <summary>
/// Adds container and Kubernetes details to the <c>/appinfo</c> response.
/// </summary>
public static class ContainerAppInfoBuilderExtensions
{
    /// <summary>
    /// Adds <c>Container</c>: <c>IsContainer</c>, <c>IsKubernetes</c>, <c>PodName</c>, <c>Namespace</c> and <c>NodeName</c>.
    /// </summary>
    /// <remarks>
    /// Detection uses environment variables (<c>KUBERNETES_SERVICE_HOST</c>, <c>DOTNET_RUNNING_IN_CONTAINER</c> and the
    /// variables in <see cref="ContainerDetailsOptions"/>) and marker files (<c>/.dockerenv</c>, <c>/run/.containerenv</c>,
    /// the service account's <c>namespace</c> file). It runs once, on the first request, and never fails the request.
    /// Safe to call more than once: the field is added once and every <paramref name="configure"/> delegate is applied.
    /// </remarks>
    /// <param name="builder">The AppInfo builder.</param>
    /// <param name="configure">Optional configuration of <see cref="ContainerDetailsOptions"/>.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IAppInfoBuilder WithContainerDetails(this IAppInfoBuilder builder, Action<ContainerDetailsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOptions<ContainerDetailsOptions>();
        if (configure is not null)
        {
            builder.Services.Configure(configure);
        }

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IAppInfoContributor, ContainerAppInfoContributor>(
            services => new ContainerAppInfoContributor(
                services.GetRequiredService<IOptions<ContainerDetailsOptions>>(),
                services.GetService<ContainerHost>() ?? ContainerHost.Current)));
        return builder;
    }
}
