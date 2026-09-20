namespace OvertonesPlayground.Tests.TestSupport;

///<summary>
///An <see cref="IFileSystem"/> whose app-data and cache folders are fresh temp folders, deleted when the test is done.
///Handing it to a service or view model lets it write and read real files without going near the device.
///</summary>
internal sealed class TempFileSystem : IFileSystem, IDisposable
{
    #region Fields
    private readonly Dictionary<string, byte[]> _package = new(StringComparer.Ordinal);
    private readonly string _root = Directory.CreateTempSubdirectory("overtones-tests-").FullName;
    #endregion

    #region Constructors
    public TempFileSystem()
    {
        AppDataDirectory = Directory.CreateDirectory(Path.Combine(_root, "appdata")).FullName;
        CacheDirectory = Directory.CreateDirectory(Path.Combine(_root, "cache")).FullName;
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Puts a file in the fake app package, as a bundled MauiAsset would be.
    ///</summary>
    public void AddPackageFile(string name, byte[] contents) => _package[name] = contents;

    public Task<bool> AppPackageFileExistsAsync(string filename) => Task.FromResult(_package.ContainsKey(filename));

    ///<summary>
    ///Writes a placeholder file inside the app-data folder and returns its path, for tests that only need a file to
    ///exist.
    ///</summary>
    public string CreateFile(string name, string contents = "x")
    {
        string path = Path.Combine(AppDataDirectory, name);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return path;
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    ///<summary>
    ///A path inside the app-data folder, without creating anything.
    ///</summary>
    public string InAppData(params string[] parts) => Path.Combine([AppDataDirectory, .. parts]);

    ///<summary>
    ///Serves a package file through a stream that cannot seek, like an Android asset stream; throws for a file that was
    ///not added.
    ///</summary>
    public Task<Stream> OpenAppPackageFileAsync(string filename)
    {
        if (!_package.TryGetValue(filename, out byte[]? contents))
        {
            throw new FileNotFoundException("The test file system has no such app package file.", filename);
        }

        PackageOpenCount++;
        return Task.FromResult<Stream>(new ForwardOnlyStream(new MemoryStream(contents)));
    }
    #endregion

    #region Public properties
    public string AppDataDirectory { get; }

    public string CacheDirectory { get; }

    ///<summary>
    ///How many times a file was opened from the fake app package.
    ///</summary>
    public int PackageOpenCount { get; private set; }
    #endregion

    private sealed class ForwardOnlyStream(Stream inner) : Stream
    {
        #region Protected methods
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
        #endregion

        #region Public methods
        public override void Flush()
        {
        }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        #endregion

        #region Public properties
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        #endregion
    }
}
