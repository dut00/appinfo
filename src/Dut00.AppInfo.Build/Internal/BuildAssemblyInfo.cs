using System.Reflection;

namespace Dut00.AppInfo.Build.Internal;

/// <summary>
/// The build attributes of the entry assembly. Not registered by default; tests register their own value to replace it,
/// because under a test runner the entry assembly is the test project.
/// </summary>
/// <param name="InformationalVersion">The trimmed <see cref="AssemblyInformationalVersionAttribute"/> value, or <see langword="null"/>.</param>
/// <param name="Metadata">Trimmed, non-blank <see cref="AssemblyMetadataAttribute"/> values by key, ignoring case; the last attribute wins.</param>
internal sealed record BuildAssemblyInfo(string? InformationalVersion, IReadOnlyDictionary<string, string> Metadata)
{
    private static readonly Lazy<BuildAssemblyInfo> EntryAssembly = new(() => FromAssembly(Assembly.GetEntryAssembly()));

    public static BuildAssemblyInfo Current => EntryAssembly.Value;

    internal static BuildAssemblyInfo FromAssembly(Assembly? assembly) => Create(
        assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        assembly?.GetCustomAttributes<AssemblyMetadataAttribute>().Select(a => (a.Key, a.Value)) ?? []);

    internal static BuildAssemblyInfo Create(string? informationalVersion, IEnumerable<(string Key, string? Value)> metadata)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in metadata)
        {
            if (BuildAppInfoContributor.Normalize(value) is { } normalized)
            {
                values[key] = normalized;
            }
        }

        return new(BuildAppInfoContributor.Normalize(informationalVersion), values);
    }
}
