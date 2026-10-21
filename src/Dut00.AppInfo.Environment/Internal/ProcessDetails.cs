namespace Dut00.AppInfo.Environment.Internal;

/// <summary>
/// The <c>Process</c> group. Properties are serialized in declaration order.
/// </summary>
/// <param name="Id">The process ID.</param>
/// <param name="StartTime">The start time in UTC, preformatted, or <see langword="null"/> when unknown.</param>
/// <param name="Uptime">The time since the process started, preformatted, or <see langword="null"/> when the start time is unknown.</param>
internal sealed record ProcessDetails(int Id, string? StartTime, string? Uptime);
