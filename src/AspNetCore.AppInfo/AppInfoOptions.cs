using System.Text.Json;

namespace AspNetCore.AppInfo;

/// <summary>
/// Options for the <c>/appinfo</c> endpoint.
/// </summary>
public sealed class AppInfoOptions
{
    /// <summary>
    /// Serializer options for the response. By default output is indented and keys are emitted as-is (PascalCase).
    /// </summary>
    /// <remarks>
    /// <see cref="JsonSerializerOptions.DictionaryKeyPolicy"/> is applied to top-level keys.
    /// </remarks>
    public JsonSerializerOptions JsonSerializerOptions { get; set; } = new()
    {
        WriteIndented = true,
    };
}
