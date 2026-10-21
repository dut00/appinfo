namespace Dut00.AppInfo.Build.Internal;

/// <summary>
/// Reads environment variables. Not registered by default; tests register their own reader, because environment
/// variables are shared by the whole process (and CI sets the ones this package looks for).
/// </summary>
internal sealed class EnvironmentVariables(Func<string, string?> read)
{
    public static EnvironmentVariables Current { get; } = new(System.Environment.GetEnvironmentVariable);

    public string? Get(string name) => read(name);
}
