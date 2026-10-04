namespace Dut00.AppInfo.Configuration.Tests;

/// <summary>
/// A unique directory under the system temp folder, deleted on dispose.
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = Directory.CreateDirectory(
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "appinfo-tests-" + Guid.NewGuid().ToString("N"))).FullName;
    }

    public string Path { get; }

    /// <summary>
    /// Writes a file and returns its absolute path.
    /// </summary>
    public string WriteFile(string name, string content)
    {
        var path = Combine(name);
        File.WriteAllText(path, content);
        return path;
    }

    public string Combine(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A file watcher, indexer or antivirus may still hold the directory; the OS cleans the temp folder eventually.
        }
    }
}
