using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Dut00.AppInfo.Build.Internal;

/// <summary>
/// Writes the <c>Build</c> group.
/// </summary>
internal sealed class BuildAppInfoContributor(
    IConfiguration configuration,
    IOptions<BuildDetailsOptions> options,
    BuildAssemblyInfo assembly,
    EnvironmentVariables environmentVariables) : IAppInfoContributor
{
    private readonly string? _commitFromVersion = ParseCommit(assembly.InformationalVersion);

    public ValueTask ContributeAsync(AppInfoContext context, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        context.Set("Build", new BuildDetails(
            InformationalVersion: assembly.InformationalVersion,
            Commit: Resolve(settings, "Commit", _commitFromVersion, settings.CommitEnvironmentVariables),
            Branch: Resolve(settings, "Branch", fallback: null, settings.BranchEnvironmentVariables),
            BuildId: Resolve(settings, "BuildId", fallback: null, settings.BuildIdEnvironmentVariables)));
        return ValueTask.CompletedTask;
    }

    // Configuration > assembly metadata > fallback > environment variables, first non-blank value wins.
    private string? Resolve(BuildDetailsOptions settings, string key, string? fallback, IEnumerable<string> variables)
    {
        if (Normalize(configuration[$"{settings.ConfigurationSection}:{key}"]) is { } configured)
        {
            return configured;
        }

        if (assembly.Metadata.TryGetValue(key, out var stamped))
        {
            return stamped;
        }

        if (fallback is not null)
        {
            return fallback;
        }

        foreach (var variable in variables)
        {
            if (!string.IsNullOrWhiteSpace(variable) && Normalize(environmentVariables.Get(variable)) is { } value)
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// The commit the SDK appends to the informational version: the last <c>.</c>-separated segment after the first
    /// <c>+</c>, when it is 7 to 64 hex digits (<c>1.0.5+&lt;sha&gt;</c>, <c>1.0.5+meta.&lt;sha&gt;</c>).
    /// </summary>
    internal static string? ParseCommit(string? informationalVersion)
    {
        var plus = informationalVersion?.IndexOf('+') ?? -1;
        if (plus < 0)
        {
            return null;
        }

        var metadata = informationalVersion![(plus + 1)..];
        var candidate = metadata[(metadata.LastIndexOf('.') + 1)..];
        return candidate.Length is >= 7 and <= 64 && candidate.All(char.IsAsciiHexDigit) ? candidate : null;
    }

    internal static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
