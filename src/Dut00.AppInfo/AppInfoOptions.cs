using System.Text.Json;

namespace Dut00.AppInfo;

/// <summary>
/// Options for the <c>/appinfo</c> endpoint.
/// </summary>
public sealed class AppInfoOptions
{
    private JsonNamingPolicy? _keyNamingPolicy;

    /// <summary>
    /// Serializer options for the response values. By default output is indented and names are emitted as-is (PascalCase).
    /// </summary>
    /// <remarks>
    /// Top-level keys are converted by <see cref="KeyNamingPolicy"/>, not by this instance.
    /// <see cref="JsonSerializerOptions.PropertyNamingPolicy"/> applies to properties of nested objects and
    /// <see cref="JsonSerializerOptions.DictionaryKeyPolicy"/> to keys of nested dictionaries.
    /// </remarks>
    public JsonSerializerOptions JsonSerializerOptions { get; set; } = new()
    {
        WriteIndented = true,
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
