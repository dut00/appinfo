using System.Reflection;
using Microsoft.Extensions.Hosting;

namespace AspNetCore.AppInfo.Internal;

/// <summary>
/// Writes the fields every <c>/appinfo</c> response has.
/// </summary>
internal sealed class CoreAppInfoContributor(IHostEnvironment environment) : IAppInfoContributor
{
    private static readonly string? EntryAssemblyVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString();

    public ValueTask ContributeAsync(AppInfoContext context, CancellationToken cancellationToken)
    {
        context.Set("ApplicationName", environment.ApplicationName);
        context.Set("Version", EntryAssemblyVersion);
        context.Set("Environment", environment.EnvironmentName);
        context.Set("IsProduction", environment.IsProduction());
        return ValueTask.CompletedTask;
    }
}
