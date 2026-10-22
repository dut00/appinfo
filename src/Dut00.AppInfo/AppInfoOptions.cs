using System.Text.Encodings.Web;
using System.Text.Json;

namespace Dut00.AppInfo;

/// <summary>
/// Options for the <c>/appinfo</c> endpoint.
/// </summary>
public sealed class AppInfoOptions
{
    /// <summary>
    /// Serializer options for the response values. By default output is indented, names are emitted as-is (PascalCase)
    /// and characters such as <c>+</c>, <c>&lt;</c>, <c>'</c> or non-ASCII letters are written as-is, not as <c>\uXXXX</c>.
    /// </summary>
    /// <remarks>
    /// Top-level keys are converted by <see cref="KeyNamingPolicy"/>, not by this instance.
    /// <see cref="JsonSerializerOptions.PropertyNamingPolicy"/> applies to properties of nested objects and
    /// <see cref="JsonSerializerOptions.DictionaryKeyPolicy"/> to keys of nested dictionaries.
    /// The default <see cref="JsonSerializerOptions.Encoder"/> is <see cref="JavaScriptEncoder.UnsafeRelaxedJsonEscaping"/>:
    /// the response is <c>application/json</c> meant for people and tools, not text embedded in HTML. Set it to
    /// <see cref="JavaScriptEncoder.Default"/> for the stricter escaping.
    /// Replacing this instance discards these defaults (indentation and the relaxed encoder): modify it in place, or set
    /// them on the new instance.
    /// </remarks>
    public JsonSerializerOptions JsonSerializerOptions { get; set; } = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Naming policy for keys, for example <see cref="JsonNamingPolicy.CamelCase"/>.
    /// <see langword="null"/> (the default) emits keys as defined (PascalCase).
    /// </summary>
    /// <remarks>
    /// Applies to top-level keys and to properties of nested objects: after the <c>Configure</c> delegates
    /// (<c>AddAppInfo(o =&gt; ...)</c>, <c>Configure&lt;AppInfoOptions&gt;</c>) have run, the endpoint uses a copy of
    /// <see cref="JsonSerializerOptions"/> whose <see cref="JsonSerializerOptions.PropertyNamingPolicy"/> is this policy.
    /// Set it in a <c>Configure</c> delegate, not in <c>PostConfigure</c>. Within those delegates the order of assignments
    /// doesn't matter, and the instance you assign is never modified, so read-only instances such as
    /// <see cref="JsonSerializerOptions.Default"/> work too. Keys of nested dictionaries, such as connection string names,
    /// are user data and stay as-is.
    /// </remarks>
    public JsonNamingPolicy? KeyNamingPolicy { get; set; }
}
