namespace Dut00.AppInfo;

/// <summary>
/// Options for the <c>Owner</c> field added by <see cref="AppInfoBuilderExtensions.WithOwner"/>.
/// </summary>
public sealed class OwnerOptions
{
    /// <summary>
    /// Who owns the application, for example a team name or a contact e-mail address.
    /// </summary>
    public string? Owner { get; set; }
}
