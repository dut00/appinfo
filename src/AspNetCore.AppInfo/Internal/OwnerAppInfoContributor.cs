using Microsoft.Extensions.Options;

namespace AspNetCore.AppInfo.Internal;

/// <summary>
/// Writes the <c>Owner</c> field.
/// </summary>
internal sealed class OwnerAppInfoContributor(IOptions<OwnerOptions> options) : IAppInfoContributor
{
    public ValueTask ContributeAsync(AppInfoContext context, CancellationToken cancellationToken)
    {
        context.Set("Owner", options.Value.Owner);
        return ValueTask.CompletedTask;
    }
}
