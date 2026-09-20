using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.Services.Implementations;

///<inheritdoc cref="ISampleAssetStore"/>
public class SampleAssetStore : ISampleAssetStore
{
    #region Constants
    ///<summary>
    ///How many cached copies are kept. Samples average about 0.4 MB (the largest is 37 MB), so this is a small, bounded cache.
    ///</summary>
    public const int MaxCachedFiles = 64;

    private const string FolderName = "sample-bank";
    #endregion

    #region Fields
    private readonly IFileSystem _fileSystem;
    private readonly SemaphoreSlim _gate = new(1, 1);
    #endregion

    #region Constructors
    public SampleAssetStore(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }
    #endregion

    #region Private methods
    ///<summary>
    ///The cache file name for an asset: the asset name with any character the file system rejects replaced, so the copy always
    ///has a legal name.
    ///</summary>
    private static string CacheFileName(string assetName)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return string.Concat(assetName.Select(c => Array.IndexOf(invalid, c) >= 0 ? '_' : c));
    }

    ///<summary>
    ///Deletes the least recently used copies until at most <see cref="MaxCachedFiles"/> remain, never the one just asked for.
    ///</summary>
    private static void Trim(string folder, string keepPath)
    {
        List<FileInfo> files = [.. new DirectoryInfo(folder).EnumerateFiles().OrderByDescending(file => file.LastWriteTimeUtc)];
        foreach (FileInfo stale in files.Skip(MaxCachedFiles))
        {
            bool isKept = string.Equals(stale.FullName, keepPath, StringComparison.OrdinalIgnoreCase);
            if (!isKept)
            {
                try
                {
                    stale.Delete();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // A copy that is still being played can't be deleted on some platforms; it will go next time.
                }
            }
        }
    }
    #endregion

    #region Public methods
    ///<inheritdoc/>
    public async Task<string> CopyToAsync(string assetName, string destinationDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        string cached = await GetLocalPathAsync(assetName, cancellationToken);

        _ = Directory.CreateDirectory(destinationDirectory);
        string fileName = CacheFileName(assetName);
        string stem = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        string target = Path.Combine(destinationDirectory, fileName);
        for (int number = 2; File.Exists(target); number++)
        {
            target = Path.Combine(destinationDirectory, $"{stem} ({number}){extension}");
        }

        File.Copy(cached, target);
        return target;
    }

    ///<inheritdoc/>
    public async Task<string> GetLocalPathAsync(string assetName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetName);

        string folder = Path.Combine(_fileSystem.CacheDirectory, FolderName);
        _ = Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, CacheFileName(assetName));

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(path))
            {
                // Touch it, so the trim below treats it as recently used.
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
                return path;
            }

            // Copy to a temporary name first, so an interrupted copy is never mistaken for a complete file.
            string partial = path + ".partial";
            try
            {
                using (Stream source = await _fileSystem.OpenAppPackageFileAsync(assetName))
                using (FileStream target = File.Create(partial))
                {
                    await source.CopyToAsync(target, cancellationToken);
                }

                File.Move(partial, path, overwrite: true);
            }
            catch
            {
                if (File.Exists(partial))
                {
                    File.Delete(partial);
                }

                throw;
            }

            Trim(folder, path);
            return path;
        }
        finally
        {
            _ = _gate.Release();
        }
    }
    #endregion
}
