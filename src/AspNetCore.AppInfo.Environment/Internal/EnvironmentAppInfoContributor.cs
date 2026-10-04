using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.Hosting;

namespace AspNetCore.AppInfo.Environment.Internal;

/// <summary>
/// Writes the process and host fields.
/// </summary>
internal sealed class EnvironmentAppInfoContributor(
    IHostEnvironment environment,
    TimeProvider timeProvider,
    ProcessStartTime processStartTime) : IAppInfoContributor
{
    private static readonly string? EntryAssemblyLocation = NormalizeLocation(Assembly.GetEntryAssembly()?.Location);

    public ValueTask ContributeAsync(AppInfoContext context, CancellationToken cancellationToken)
    {
        context.Set("ApplicationProcessUptime", FormatUptime(timeProvider.GetUtcNow() - processStartTime.Value));
        context.Set("HostName", System.Environment.MachineName);
        context.Set("ContentRootPath", environment.ContentRootPath);
        context.Set("AssemblyLocation", EntryAssemblyLocation);
        return ValueTask.CompletedTask;
    }

    // Assembly.Location is empty for single-file apps; written as null in that case.
    internal static string? NormalizeLocation(string? location) => string.IsNullOrEmpty(location) ? null : location;

    // Constant ("c") format: [d.]hh:mm:ss[.fffffff]. A clock behind the start time reports zero.
    internal static string FormatUptime(TimeSpan uptime) =>
        (uptime < TimeSpan.Zero ? TimeSpan.Zero : uptime).ToString("c", CultureInfo.InvariantCulture);
}
