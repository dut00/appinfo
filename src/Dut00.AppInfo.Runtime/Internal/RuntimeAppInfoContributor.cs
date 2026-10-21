using System.Runtime.InteropServices;

namespace Dut00.AppInfo.Runtime.Internal;

/// <summary>
/// Writes the <c>Runtime</c> group.
/// </summary>
internal sealed class RuntimeAppInfoContributor : IAppInfoContributor
{
    // The values can't change while the process runs.
    internal static readonly RuntimeDetails Current = new(
        Framework: RuntimeInformation.FrameworkDescription,
        RuntimeIdentifier: RuntimeInformation.RuntimeIdentifier,
        OSDescription: RuntimeInformation.OSDescription,
        OSArchitecture: RuntimeInformation.OSArchitecture.ToString(),
        ProcessArchitecture: RuntimeInformation.ProcessArchitecture.ToString());

    public ValueTask ContributeAsync(AppInfoContext context, CancellationToken cancellationToken)
    {
        context.Set("Runtime", Current);
        return ValueTask.CompletedTask;
    }
}
