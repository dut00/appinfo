using Dut00.AppInfo.Build.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Dut00.AppInfo.Build;

/// <summary>
/// Adds build details to the <c>/appinfo</c> response.
/// </summary>
public static class BuildAppInfoBuilderExtensions
{
    /// <summary>
    /// Adds <c>Build</c>: <c>InformationalVersion</c>, <c>Commit</c>, <c>Branch</c> and <c>BuildId</c>.
    /// </summary>
    /// <remarks>
    /// Each value comes from the first source that has it: configuration (<c>Build:Commit</c>, ...), then
    /// <c>[AssemblyMetadata("Commit", ...)]</c> on the entry assembly, then (for the commit) the git commit the SDK appends to
    /// the informational version, then well-known CI environment variables. Missing values are <see langword="null"/>.
    /// Safe to call more than once: the field is added once and every <paramref name="configure"/> delegate is applied.
    /// </remarks>
    /// <param name="builder">The AppInfo builder.</param>
    /// <param name="configure">Optional configuration of <see cref="BuildDetailsOptions"/>.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IAppInfoBuilder WithBuildDetails(this IAppInfoBuilder builder, Action<BuildDetailsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddOptions<BuildDetailsOptions>();
        if (configure is not null)
        {
            builder.Services.Configure(configure);
        }

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IAppInfoContributor, BuildAppInfoContributor>(
            services => new BuildAppInfoContributor(
                services.GetRequiredService<IConfiguration>(),
                services.GetRequiredService<IOptions<BuildDetailsOptions>>(),
                services.GetService<BuildAssemblyInfo>() ?? BuildAssemblyInfo.Current,
                services.GetService<EnvironmentVariables>() ?? EnvironmentVariables.Current)));
        return builder;
    }
}
