using OvertonesPlayground.Ontology.Analysis;
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
    ///<summary>
    ///Writes the cached sample to <paramref name="target"/> in a form every library tool can open. The editor, trim, mixer and
    ///multi-track tools read 16-bit PCM only (<see cref="WavFile"/>), while most bundled samples are 24-bit or
    ///<c>WAVE_FORMAT_EXTENSIBLE</c>, so those are converted to 16-bit PCM (rounded and clamped; sample rate and channels are
    ///kept). A plain 16-bit PCM file is copied byte for byte.
    ///</summary>
    private static async Task WriteAsLibraryClipAsync(string source, string target, CancellationToken cancellationToken)
    {
        byte[] bytes = await File.ReadAllBytesAsync(source, cancellationToken);
        WavData? wav = null;
        try
        {
            wav = RiffWavReader.Read(bytes);
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidDataException)
        {
            // Not a WAV the reader understands (for example an AIFF): hand it over as it is rather than lose the sound.
        }

        if (wav is null || wav.Format is { BitsPerSample: 16, IsFloat: false, IsExtensible: false })
        {
            await File.WriteAllBytesAsync(target, bytes, cancellationToken);
            return;
        }

        float[][] channels = wav.Audio.Channels;
        int frames = wav.Audio.FrameCount;
        short[] interleaved = new short[frames * channels.Length];
        for (int frame = 0; frame < frames; frame++)
        {
            for (int channel = 0; channel < channels.Length; channel++)
            {
                interleaved[(frame * channels.Length) + channel] = PcmMath.ClampToShort(channels[channel][frame] * 32767.0);
            }
        }

        WavFile converted = new()
        {
            Channels = (short)channels.Length,
            SampleRate = wav.Audio.SampleRate,
            BitsPerSample = 16,
            Samples = interleaved,
        };
        await converted.WriteAsync(target);
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

        await WriteAsLibraryClipAsync(cached, target, cancellationToken);
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
