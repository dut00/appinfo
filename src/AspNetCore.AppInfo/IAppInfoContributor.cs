namespace AspNetCore.AppInfo;

/// <summary>
/// Adds keys to the <c>/appinfo</c> response.
/// </summary>
/// <remarks>
/// Contributors are resolved from DI on every request and run in registration order.
/// Register custom contributors with <see cref="AppInfoBuilderExtensions.WithContributor{T}"/>.
/// </remarks>
public interface IAppInfoContributor
{
    /// <summary>
    /// Writes this contributor's keys into <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The response being built.</param>
    /// <param name="cancellationToken">Signals that the request was aborted.</param>
    ValueTask ContributeAsync(AppInfoContext context, CancellationToken cancellationToken);
}
