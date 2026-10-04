namespace Dut00.AppInfo.Internal;

/// <summary>
/// Holds the value passed to <see cref="AppInfoBuilderExtensions.WithOwner"/>. The last call wins.
/// </summary>
internal sealed class OwnerAppInfoOptions
{
    public string? Owner { get; set; }
}
