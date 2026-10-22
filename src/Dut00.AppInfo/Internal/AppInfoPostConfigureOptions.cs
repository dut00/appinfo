using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Dut00.AppInfo.Internal;

/// <summary>
/// Applies <see cref="AppInfoOptions.KeyNamingPolicy"/> to nested properties once every <c>configure</c> delegate has run.
/// </summary>
internal sealed class AppInfoPostConfigureOptions : IPostConfigureOptions<AppInfoOptions>
{
    public void PostConfigure(string? name, AppInfoOptions options)
    {
        if (options.KeyNamingPolicy is { } policy && options.JsonSerializerOptions.PropertyNamingPolicy != policy)
        {
            // A copy: the configured instance may be read-only (JsonSerializerOptions.Web, or one that has already
            // serialized something) or shared with other code.
            options.JsonSerializerOptions = new JsonSerializerOptions(options.JsonSerializerOptions) { PropertyNamingPolicy = policy };
        }
    }
}
