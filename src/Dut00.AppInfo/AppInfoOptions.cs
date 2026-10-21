using System.Text.Encodings.Web;
using System.Text.Json;

namespace Dut00.AppInfo;

/// <summary>
/// Options for the <c>/appinfo</c> endpoint.
/// </summary>
public sealed class AppInfoOptions
{
    private JsonNamingPolicy? _keyNamingPolicy;

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
    /// Applies to top-level keys and, by also setting <see cref="JsonSerializerOptions.PropertyNamingPolicy"/> on the
    /// current <see cref="JsonSerializerOptions"/> instance, to properties of nested objects. Keys of nested dictionaries,
    /// such as connection string names, are user data and stay as-is. Replacing <see cref="JsonSerializerOptions"/>
    /// afterwards keeps the top-level policy but discards the nested one.
    /// </remarks>
    public JsonNamingPolicy? KeyNamingPolicy
    {
        get => _keyNamingPolicy;
        set
        {
            _keyNamingPolicy = value;
            JsonSerializerOptions.PropertyNamingPolicy = value;
        }
    }
}
