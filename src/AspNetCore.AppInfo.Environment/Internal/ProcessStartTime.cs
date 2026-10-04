using System.ComponentModel;
using System.Diagnostics;

namespace AspNetCore.AppInfo.Environment.Internal;

/// <summary>
/// When the current process started. Not registered by default; tests register their own value to replace it.
/// </summary>
internal sealed record ProcessStartTime(DateTimeOffset Value)
{
    private static readonly Lazy<ProcessStartTime> CurrentProcess = new(Read);

    public static ProcessStartTime Current => CurrentProcess.Value;

    private static ProcessStartTime Read()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return new(new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero));
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or Win32Exception)
        {
            // The platform doesn't expose the start time; count from the first time it was needed instead.
            return new(TimeProvider.System.GetUtcNow());
        }
    }
}
