using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace Dut00.AppInfo.Server.Internal;

/// <summary>
/// Writes the <c>Urls</c> field.
/// </summary>
internal sealed class ServerAppInfoContributor(IServer? server) : IAppInfoContributor
{
    public ValueTask ContributeAsync(AppInfoContext context, CancellationToken cancellationToken)
    {
        context.Set("Urls", ReadAddresses(server));
        return ValueTask.CompletedTask;
    }

    // A copy, so the response never shares the server's live collection. It is copied by enumerating: Kestrel can
    // rebind after a configuration reload, and its enumerator takes a snapshot under a lock, while Count + CopyTo
    // (what ToArray and the list constructor do) are two separate calls that a rebind can fall between.
    internal static string[]? ReadAddresses(IServer? server)
    {
        if (server?.Features.Get<IServerAddressesFeature>()?.Addresses is not { } addresses)
        {
            return null;
        }

        var copy = new List<string>();
        foreach (var address in addresses)
        {
            copy.Add(address);
        }

        return [.. copy];
    }
}
