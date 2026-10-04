using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace Dut00.AppInfo.Configuration.Internal;

/// <summary>
/// Writes the <c>ConfigurationsFiles</c> field.
/// </summary>
internal sealed class ConfigurationAppInfoContributor(IConfiguration configuration, IOptions<ConfigurationDetailsOptions> options)
    : IAppInfoContributor
{
    public ValueTask ContributeAsync(AppInfoContext context, CancellationToken cancellationToken)
    {
        // Computed per request: files can appear or disappear while the application runs.
        context.Set("ConfigurationsFiles", GetConfigurationFiles(configuration, options.Value.IncludeMissingOptionalFiles));
        return ValueTask.CompletedTask;
    }

    internal static string[] GetConfigurationFiles(IConfiguration configuration, bool includeMissingOptionalFiles)
    {
        var files = new List<string>();
        Collect(configuration, includeMissingOptionalFiles, files, new HashSet<IConfiguration>(ReferenceEqualityComparer.Instance));
        return [.. files];
    }

    private static void Collect(IConfiguration configuration, bool includeMissingOptionalFiles, List<string> files, HashSet<IConfiguration> visited)
    {
        if (configuration is not IConfigurationRoot root || !visited.Add(root))
        {
            return;
        }

        foreach (var provider in root.Providers)
        {
            switch (provider)
            {
                case FileConfigurationProvider { Source: { Path: { Length: > 0 } path } source }:
                    var file = source.FileProvider?.GetFileInfo(path);
                    var exists = file?.Exists == true;
                    if (exists || !source.Optional || includeMissingOptionalFiles)
                    {
                        files.Add(ResolvePath(path, file, source.FileProvider));
                    }

                    break;

                // builder.Configuration.AddConfiguration(otherConfiguration) wraps another root.
                case ChainedConfigurationProvider chained:
                    Collect(chained.Configuration, includeMissingOptionalFiles, files, visited);
                    break;
            }
        }
    }

    private static string ResolvePath(string path, IFileInfo? file, IFileProvider? fileProvider)
    {
        if (!string.IsNullOrEmpty(file?.PhysicalPath))
        {
            return file.PhysicalPath;
        }

        // A missing file has no physical path, but a physical provider still tells where it would be.
        // Like PhysicalFileProvider.GetFileInfo, ignore this platform's leading separators so the path stays under
        // the root ("\" is a separator on Windows only).
        if (fileProvider is PhysicalFileProvider physical)
        {
            return Path.GetFullPath(Path.Combine(physical.Root, path.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
        }

        return path;
    }
}
