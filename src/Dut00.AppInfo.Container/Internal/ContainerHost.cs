namespace Dut00.AppInfo.Container.Internal;

/// <summary>
/// Access to the host: environment variables and files. Not registered by default; tests register their own value to
/// replace it, because environment variables are shared by the whole process and the files are absolute paths.
/// </summary>
/// <param name="GetEnvironmentVariable">Reads an environment variable.</param>
/// <param name="FileExists">Whether a file exists. Must not throw.</param>
/// <param name="ReadFile">Reads a whole text file. May throw; the caller treats a failure as a missing file.</param>
/// <param name="IsWindows">Whether the host runs Windows, where the Linux marker files don't apply.</param>
internal sealed record ContainerHost(
    Func<string, string?> GetEnvironmentVariable,
    Func<string, bool> FileExists,
    Func<string, string> ReadFile,
    bool IsWindows)
{
    public static ContainerHost Current { get; } = new(
        System.Environment.GetEnvironmentVariable,
        File.Exists,
        File.ReadAllText,
        OperatingSystem.IsWindows());
}
