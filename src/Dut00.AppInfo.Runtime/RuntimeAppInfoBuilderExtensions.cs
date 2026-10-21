using Dut00.AppInfo.Runtime.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dut00.AppInfo.Runtime;

/// <summary>
/// Adds .NET runtime and operating system details to the <c>/appinfo</c> response.
/// </summary>
public static class RuntimeAppInfoBuilderExtensions
{
    /// <summary>
    /// Adds <c>Runtime</c>: <c>Framework</c>, <c>RuntimeIdentifier</c>, <c>OSDescription</c>, <c>OSArchitecture</c> and
    /// <c>ProcessArchitecture</c>.
    /// </summary>
    /// <remarks>
    /// The values come from <see cref="System.Runtime.InteropServices.RuntimeInformation"/> and are computed once.
    /// Safe to call more than once.
    /// </remarks>
    /// <param name="builder">The AppInfo builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IAppInfoBuilder WithRuntimeDetails(this IAppInfoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IAppInfoContributor, RuntimeAppInfoContributor>());
        return builder;
    }
}
