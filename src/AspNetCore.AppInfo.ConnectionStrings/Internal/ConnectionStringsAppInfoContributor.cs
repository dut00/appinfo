using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AspNetCore.AppInfo.ConnectionStrings.Internal;

/// <summary>
/// Writes the <c>ConnectionStrings</c> field.
/// </summary>
internal sealed class ConnectionStringsAppInfoContributor(IConfiguration configuration, IOptions<ConnectionStringsOptions> options)
    : IAppInfoContributor
{
    private const string SectionName = "ConnectionStrings";

    public ValueTask ContributeAsync(AppInfoContext context, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var masked = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var child in configuration.GetSection(SectionName).GetChildren())
        {
            // A child with nested keys has no value of its own; it isn't a connection string.
            if (child.Value is { } value)
            {
                masked[child.Key] = ConnectionStringMasker.Mask(value, settings.SensitiveKeys, settings.Mask);
            }
        }

        context.Set("ConnectionStrings", masked);
        return ValueTask.CompletedTask;
    }
}
