using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.Services.Implementations;

///<inheritdoc cref="IVideoConverterService"/>
///<remarks>
///Reuses <see cref="IAudioFormatConverterService"/>'s decoder to do the actual work: <c>MediaExtractor</c> demuxes
///whatever container it's given and hands the first audio track to <c>MediaCodec</c>, so decoding a video's audio track
///to WAV is exactly the same operation as decoding a compressed audio file - only the container differs.
///</remarks>
public class VideoConverterService : IVideoConverterService
{
    #region Constants
    ///<summary>The share of the whole conversion (0 to 1) taken by bringing the picked video into app storage.</summary>
    private const double CopyShare = 0.1;

    ///<summary>Where the conversion is up to (0 to 1) once the audio track has been extracted to WAV, when the target
    ///format needs no further encoding (<see cref="VideoConversionFormat.Wav"/>).</summary>
    private const double DecodeEndForWav = 0.95;

    ///<summary>Where the conversion is up to (0 to 1) once the audio track has been extracted to WAV, when it still has
    ///to be encoded to a compressed format (<see cref="VideoConversionFormat.Mp3"/>).</summary>
    private const double DecodeEndForMp3 = 0.7;

    ///<summary>Where the conversion is up to (0 to 1) once encoding to a compressed format has finished.</summary>
    private const double EncodeEnd = 0.95;

    ///<summary>Where the conversion is up to (0 to 1) once the output file is ready and only saving it remains.</summary>
    private const double FinishingStart = 0.95;

    private static readonly string[] VideoMimeTypes = ["video/mp4"];
    #endregion

    #region Fields
    private readonly IFilePicker _filePicker;
    private readonly IFileSystem _fileSystem;
    private readonly IAudioFormatConverterService _formatConverterService;
    private readonly IPublicStorageService _publicStorageService;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the video converter service.
    ///</summary>
    ///<param name="filePicker">Shows the system file picker when the user chooses a video to convert.</param>
    ///<param name="fileSystem">Locates the cache folder the picker copies the chosen video into.</param>
    ///<param name="formatConverterService">Does the actual decoding (video's audio track to WAV) and, for MP3, encoding.</param>
    ///<param name="publicStorageService">Exports the converted file into the shared Music folder.</param>
    public VideoConverterService(IFilePicker filePicker, IFileSystem fileSystem, IAudioFormatConverterService formatConverterService, IPublicStorageService publicStorageService)
    {
        _filePicker = filePicker;
        _fileSystem = fileSystem;
        _formatConverterService = formatConverterService;
        _publicStorageService = publicStorageService;
    }
    #endregion

    #region Private methods
    ///<summary>
    ///Stages the picked video, extracts its audio track, encodes it if needed, and exports the result. Reports overall
    ///conversion progress as it goes, always in order, because it runs on a single thread.
    ///</summary>
    private async Task<VideoConversionOutcome> ConvertAsync(FileResult result, string workingPath, string outputName, VideoConversionFormat format, IProgress<ImportProgress>? progress)
    {
        await PickedFileStager.StageAsync(result, workingPath, _fileSystem, fraction => progress?.Report(new ImportProgress(ImportStage.Copying, fraction * CopyShare)));

        double decodeEnd = format == VideoConversionFormat.Mp3 ? DecodeEndForMp3 : DecodeEndForWav;
        string wavPath;
        try
        {
            SyncProgress decodeProgress = new(fraction => progress?.Report(new ImportProgress(ImportStage.Decoding, CopyShare + (fraction * (decodeEnd - CopyShare)))));
            wavPath = await _formatConverterService.ConvertToWavAsync(workingPath, outputName, decodeProgress);
        }
        finally
        {
            TryDelete(workingPath);
        }

        string finalPath = wavPath;
        if (format == VideoConversionFormat.Mp3)
        {
            progress?.Report(new ImportProgress(ImportStage.Encoding, decodeEnd));
            try
            {
                finalPath = await _formatConverterService.ConvertFromWavAsync(wavPath, AudioExportFormat.Mp3, outputName);
            }
            finally
            {
                TryDelete(wavPath);
            }

            progress?.Report(new ImportProgress(ImportStage.Encoding, EncodeEnd));
        }

        progress?.Report(new ImportProgress(ImportStage.Finishing, FinishingStart));
        string fileName = Path.GetFileName(finalPath);
        string? location = await _publicStorageService.ExportToMusicAsync(finalPath, fileName);
        return new VideoConversionOutcome(fileName, location);
    }

    ///<summary>
    ///Deletes a working file, ignoring the failure; cleanup shouldn't mask whatever result or exception is already in
    ///hand, and a leftover temp file isn't worth blocking on.
    ///</summary>
    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
    }
    #endregion

    #region Private properties
    ///<summary>
    ///App-private cache folder where a picked video is copied while its audio track is extracted; cleared as soon as
    ///decoding finishes, so this never accumulates video files.
    ///</summary>
    private string WorkingDirectory
    {
        get
        {
            string dir = Path.Combine(_fileSystem.CacheDirectory, "VideoImports");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
    #endregion

    #region Public methods
    ///<inheritdoc/>
    public async Task<VideoConversionOutcome?> ConvertFromPickerAsync(VideoConversionFormat format, IProgress<ImportProgress>? progress = null)
    {
        Dictionary<DevicePlatform, IEnumerable<string>> fileTypes = new() { { DevicePlatform.Android, VideoMimeTypes }, };
        FilePickerFileType videoFileType = new(fileTypes);

        PickOptions options = new() { PickerTitle = "Choose an MP4 video", FileTypes = videoFileType, };
        FileResult? result = await _filePicker.PickAsync(options);

        if (result is null)
        {
            return null;
        }

        string workingPath = Path.Combine(WorkingDirectory, $"{Guid.NewGuid():N}_{result.FileName}");
        string outputName = Path.GetFileNameWithoutExtension(result.FileName);

        // Staging and decoding a large video take long enough to freeze the UI (and so the progress bar), so they run on
        // a pool thread. Awaiting from here puts the rest back on the caller's thread.
        return await Task.Run(() => ConvertAsync(result, workingPath, outputName, format, progress));
    }
    #endregion
}
