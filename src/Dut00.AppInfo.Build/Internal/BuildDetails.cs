namespace Dut00.AppInfo.Build.Internal;

/// <summary>
/// The <c>Build</c> group. Properties are serialized in declaration order.
/// </summary>
/// <param name="InformationalVersion">The entry assembly's informational version, for example <c>1.0.5+a1b2c3d</c>.</param>
/// <param name="Commit">The git commit.</param>
/// <param name="Branch">The git branch.</param>
/// <param name="BuildId">The CI build or run ID.</param>
internal sealed record BuildDetails(string? InformationalVersion, string? Commit, string? Branch, string? BuildId);
