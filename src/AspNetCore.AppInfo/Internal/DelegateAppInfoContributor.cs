namespace AspNetCore.AppInfo.Internal;

/// <summary>
/// Writes a single custom key whose value comes from a delegate.
/// </summary>
internal sealed class DelegateAppInfoContributor(string key, Func<IServiceProvider, object?> valueFactory) : IAppInfoContributor
{
    public ValueTask ContributeAsync(AppInfoContext context, CancellationToken cancellationToken)
    {
        context.Set(key, valueFactory(context.Services));
        return ValueTask.CompletedTask;
    }
}
