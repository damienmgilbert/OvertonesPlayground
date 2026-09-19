namespace OvertonesPlayground.Tests.TestSupport;

/// <summary>
/// An <see cref="IFileSystem"/> whose app-data and cache folders are fresh temp folders, deleted when the test is done. Handing it
/// to a service or view model lets it write and read real files without going near the device.
/// </summary>
internal sealed class TempFileSystem : IFileSystem, IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("overtones-tests-").FullName;

    public TempFileSystem()
    {
        AppDataDirectory = Directory.CreateDirectory(Path.Combine(_root, "appdata")).FullName;
        CacheDirectory = Directory.CreateDirectory(Path.Combine(_root, "cache")).FullName;
    }

    public string AppDataDirectory { get; }

    public string CacheDirectory { get; }

    public Task<bool> AppPackageFileExistsAsync(string filename) => Task.FromResult(false);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    public Task<Stream> OpenAppPackageFileAsync(string filename) => throw new FileNotFoundException("The test file system has no app package.", filename);

    /// <summary>
    /// A path inside the app-data folder, without creating anything.
    /// </summary>
    public string InAppData(params string[] parts) => Path.Combine([AppDataDirectory, .. parts]);

    /// <summary>
    /// Writes a placeholder file inside the app-data folder and returns its path, for tests that only need a file to exist.
    /// </summary>
    public string CreateFile(string name, string contents = "x")
    {
        string path = Path.Combine(AppDataDirectory, name);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return path;
    }
}
