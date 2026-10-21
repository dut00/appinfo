using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.Hosting;

namespace Dut00.AppInfo.Environment.Internal;

/// <summary>
/// Writes the process and host fields.
/// </summary>
internal sealed class EnvironmentAppInfoContributor(IHostEnvironment environment, TimeProvider timeProvider) : IAppInfoContributor
{
    private static readonly string? EntryAssemblyLocation = NormalizeLocation(Assembly.GetEntryAssembly()?.Location);

    /// <summary>
    /// When the current process started, or <see langword="null"/> when the platform doesn't expose it.
    /// </summary>
    internal static readonly DateTimeOffset? ProcessStartTime = ReadProcessStartTime();

    public ValueTask ContributeAsync(AppInfoContext context, CancellationToken cancellationToken)
    {
        context.Set("Process", CreateProcessDetails(ProcessStartTime, timeProvider.GetUtcNow()));
        context.Set("HostName", System.Environment.MachineName);
        context.Set("ContentRootPath", environment.ContentRootPath);
        context.Set("AssemblyLocation", EntryAssemblyLocation);
        return ValueTask.CompletedTask;
    }

    // StartTime uses the round-trip ("O") format in UTC: always seven fraction digits and a Z, whatever the JSON options say.
    // Without a start time, neither the start time nor the uptime is known.
    internal static ProcessDetails CreateProcessDetails(DateTimeOffset? startTime, DateTimeOffset now) => new(
        Id: System.Environment.ProcessId,
        StartTime: startTime?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
        Uptime: startTime is { } start ? FormatUptime(now - start) : null);

    // Assembly.Location is empty for single-file apps; written as null in that case.
    internal static string? NormalizeLocation(string? location) => string.IsNullOrEmpty(location) ? null : location;

    // Constant ("c") format: [d.]hh:mm:ss[.fffffff]. A clock behind the start time reports zero.
    internal static string FormatUptime(TimeSpan uptime) =>
        (uptime < TimeSpan.Zero ? TimeSpan.Zero : uptime).ToString("c", CultureInfo.InvariantCulture);

    private static DateTimeOffset? ReadProcessStartTime()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or Win32Exception)
        {
            return null;
        }
    }
}
