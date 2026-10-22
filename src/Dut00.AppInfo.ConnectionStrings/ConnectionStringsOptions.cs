namespace Dut00.AppInfo.ConnectionStrings;

/// <summary>
/// Options for the <c>ConnectionStrings</c> field added by
/// <see cref="ConnectionStringsAppInfoBuilderExtensions.WithConnectionStrings"/>.
/// </summary>
public sealed class ConnectionStringsOptions
{
    private string _mask = "***";

    /// <summary>
    /// Words that mark a connection string key as sensitive. A key is masked when it <em>contains</em> any of them,
    /// ignoring case, so <c>Password</c> also covers <c>Proxy Password</c> and <c>SSL Password</c>.
    /// </summary>
    /// <remarks>
    /// Defaults: <c>Password</c>, <c>Pwd</c>, <c>PSW</c>, <c>Pass</c>, <c>User ID</c>, <c>UID</c>, <c>User</c>, <c>Username</c>,
    /// <c>Key</c>, <c>AccountKey</c>, <c>SharedAccessKey</c>, <c>SharedAccessSignature</c>, <c>AccessKey</c>, <c>ApiKey</c>,
    /// <c>Secret</c>, <c>Token</c>, <c>Credential</c>, <c>Authorization</c>, <c>Signature</c>, <c>Bearer</c>, <c>Auth</c>, <c>Sig</c>,
    /// <c>Sas</c>, <c>Jwt</c>. Because matching is by substring, <c>Key</c> also covers <c>AppKey</c> and <c>private_key</c>,
    /// <c>Pass</c> covers <c>Passphrase</c> and <c>Passcode</c>, and <c>Auth</c> covers <c>Auth Code</c> and <c>AuthToken</c>.
    /// Short words also mask harmless keys such as <c>Authentication</c> or <c>Sasl Mechanism</c>; that is accepted.
    /// Add to the set to mask more keys. Removing entries can expose secrets.
    /// </remarks>
    public ISet<string> SensitiveKeys { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Password",
        "Pwd",
        "PSW",
        "Pass",
        "User ID",
        "UID",
        "User",
        "Username",
        "Key",
        "AccountKey",
        "SharedAccessKey",
        "SharedAccessSignature",
        "AccessKey",
        "ApiKey",
        "Secret",
        "Token",
        "Credential",
        "Authorization",
        "Signature",
        "Bearer",
        "Auth",
        "Sig",
        "Sas",
        "Jwt",
    };

    /// <summary>
    /// The text that replaces a masked value. Default: <c>***</c>.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public string Mask
    {
        get => _mask;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _mask = value;
        }
    }
}
