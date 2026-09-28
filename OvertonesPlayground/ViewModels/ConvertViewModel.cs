using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

///<summary>
///View model for the Convert page: turns any file the platform can extract an audio track from (compressed audio,
///or a video container like MP4) into any other supported audio format, at a chosen or size-targeted bit rate,
///without ever making the result a permanent Library clip.
///</summary>
public partial class ConvertViewModel : BaseViewModel
{
    #region Constants
    ///<summary>Bit rates below this sound audibly bad for a full mix; a target-size request that computes lower is
    ///clamped up to it instead.</summary>
    private const int MinBitRateBps = 48_000;

    ///<summary>Bit rates above this cost size for essentially no perceptible gain at AAC-LC; a target-size request
    ///that computes higher (e.g. a generous size budget for a short file) is clamped down to it instead.</summary>
    private const int MaxBitRateBps = 256_000;

    ///<summary>Multiplies a target-size request's raw bits-per-second so the encoded file lands at or under the
    ///target rather than right at it - the encoder's actual output, plus container/ADTS framing, isn't exact.</summary>
    private const double TargetSizeSafetyMargin = 0.92;
    #endregion

    #region Fields
    private readonly IAudioFormatConverterService _formatConverterService;
    private readonly IFileSystem _fileSystem;
    private readonly IFilePicker _filePicker;
    private readonly IPublicStorageService _publicStorageService;
    private TimeSpan _sourceDuration;
    private string? _sourcePath;
    private double? _targetSizeMb;
    #endregion

    #region Constructors
    public ConvertViewModel(IFilePicker filePicker, IAudioFormatConverterService formatConverterService, IPublicStorageService publicStorageService, IFileSystem fileSystem, ILogger<ConvertViewModel> logger) : base(logger)
    {
        _filePicker = filePicker;
        _formatConverterService = formatConverterService;
        _publicStorageService = publicStorageService;
        _fileSystem = fileSystem;
        Title = "Convert";
    }
    #endregion

    #region Private methods
    private static string FormatDuration(TimeSpan duration) => duration.TotalHours >= 1 ? duration.ToString(@"h\:mm\:ss") : duration.ToString(@"m\:ss");

    private static string FormatSize(long bytes)
    {
        double mb = bytes / (1024.0 * 1024.0);
        return mb >= 1024 ? $"{mb / 1024:F2} GB" : $"{mb:F1} MB";
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to convert '{FileName}' to {TargetFormat}.")]
    private partial void Log_ConvertFailed(Exception exception, string fileName, ConversionTargetFormat targetFormat);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Converting '{FileName}' to {TargetFormat} at {BitRateBps} bps.")]
    private partial void Log_Converting(string fileName, ConversionTargetFormat targetFormat, int bitRateBps);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to read a picked file's format.")]
    private partial void Log_PickFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Picked '{FileName}' for conversion.")]
    private partial void Log_Picked(string fileName);

    partial void OnBitRateBpsChanged(int value) => BitRateSummary = $"≈{value / 1000} kbps";

    ///<summary>
    ///Parses <see cref="TargetSizeText"/> (blank or unparsable both mean "no target size") and recomputes
    ///<see cref="BitRateBps"/> from it.
    ///</summary>
    partial void OnTargetSizeTextChanged(string value)
    {
        _targetSizeMb = double.TryParse(value, out double parsed) && parsed > 0 ? parsed : null;
        RecomputeBitRateFromTargetSize();
    }

    ///<summary>
    ///Recomputes <see cref="BitRateBps"/> from the parsed <see cref="TargetSizeText"/> and the picked file's
    ///duration. Does nothing if either isn't known yet, leaving whatever bit rate (preset or previous computation)
    ///already set.
    ///</summary>
    private void RecomputeBitRateFromTargetSize()
    {
        bool canCompute = _targetSizeMb is > 0 && _sourceDuration > TimeSpan.Zero;
        if (!canCompute)
        {
            return;
        }

        double targetBits = _targetSizeMb!.Value * 1024 * 1024 * 8;
        double computedBps = targetBits / _sourceDuration.TotalSeconds * TargetSizeSafetyMargin;
        BitRateBps = (int)Math.Clamp(computedBps, MinBitRateBps, MaxBitRateBps);
    }

    ///<summary>
    ///Returns a real filesystem path for <paramref name="result"/>: its own path if the picker already exposes one
    ///(the normal case on Android - the picker copies the chosen content into the app's cache), otherwise copies its
    ///stream into a scratch file first, since <c>MediaExtractor</c> needs an actual path, not a stream.
    ///</summary>
    private async Task<string> ResolveSourcePathAsync(FileResult result)
    {
        bool hasUsablePath = !string.IsNullOrEmpty(result.FullPath) && File.Exists(result.FullPath);
        if (hasUsablePath)
        {
            return result.FullPath;
        }

        string stagingDirectory = Path.Combine(_fileSystem.CacheDirectory, "Convert");
        Directory.CreateDirectory(stagingDirectory);
        string destination = Path.Combine(stagingDirectory, $"{Guid.NewGuid():N}_{result.FileName}");

        await using Stream source = await result.OpenReadAsync();
        await using FileStream dest = File.Create(destination);
        await source.CopyToAsync(dest);
        return destination;
    }

    ///<summary>
    ///Best-effort delete of a file this view model created itself (a temporary decoded WAV, or an encoded result
    ///already copied to shared storage) - never the user's own picked file.
    ///</summary>
    private static void TryDeleteBestEffort(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }

    private bool CanConvert() => HasSource && !IsConverting;

    ///<summary>
    ///Decodes the picked file (if it isn't already WAV), then either exports that WAV directly (a
    ///<see cref="ConversionTargetFormat.Wav"/> target) or encodes it to the chosen compressed format at
    ///<see cref="BitRateBps"/>, and copies the result into shared storage. Any intermediate file this method itself
    ///created is deleted afterward regardless of outcome; the user's original file is never touched.
    ///</summary>
    [RelayCommand(CanExecute = nameof(CanConvert))]
    private async Task ConvertAsync()
    {
        if (_sourcePath is null)
        {
            return;
        }

        IsConverting = true;
        ConvertFraction = 0;
        ConvertStatus = "Preparing...";
        StatusMessage = null;

        string clipName = Path.GetFileNameWithoutExtension(SourceFileName ?? "converted");
        string? tempWavPath = null;
        string? encodedPath = null;
        try
        {
            Log_Converting(clipName, TargetFormat, BitRateBps);

            string wavPath;
            if (_formatConverterService.NeedsConversion(_sourcePath))
            {
                ConvertStatus = "Converting to WAV...";
                double decodeShare = TargetFormat == ConversionTargetFormat.Wav ? 1.0 : 0.5;
                Progress<double> decodeProgress = new(fraction =>
                {
                    ConvertFraction = fraction * decodeShare;
                    ConvertStatus = $"Converting to WAV... {fraction:P0}";
                });

                wavPath = await _formatConverterService.ConvertToWavAsync(_sourcePath, clipName, decodeProgress);
                tempWavPath = wavPath;
            }
            else
            {
                wavPath = _sourcePath;
            }

            string resultPath;
            if (TargetFormat == ConversionTargetFormat.Wav)
            {
                resultPath = wavPath;
            }
            else
            {
                AudioExportFormat exportFormat = TargetFormat == ConversionTargetFormat.Aac ? AudioExportFormat.Aac : AudioExportFormat.Mp3;
                double encodeBaseFraction = tempWavPath is null ? 0 : 0.5;
                Progress<double> encodeProgress = new(fraction =>
                {
                    ConvertFraction = encodeBaseFraction + (fraction * (1 - encodeBaseFraction));
                    ConvertStatus = $"Encoding to {TargetFormat}... {fraction:P0}";
                });

                resultPath = await _formatConverterService.ConvertFromWavAsync(wavPath, exportFormat, clipName, BitRateBps, encodeProgress);
                encodedPath = resultPath;
            }

            string? location = await _publicStorageService.ExportToMusicAsync(resultPath, Path.GetFileName(resultPath));
            StatusMessage = location is not null
                ? $"Converted '{clipName}' to {location}."
                : $"Converted '{clipName}', but couldn't save it to shared storage.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            Log_ConvertFailed(ex, clipName, TargetFormat);
            StatusMessage = TargetFormat == ConversionTargetFormat.Mp3
                ? $"Couldn't convert '{clipName}' to MP3 - many Android devices don't have an MP3 encoder. Try AAC instead."
                : $"Couldn't convert '{clipName}'.";
        }
        finally
        {
            if (tempWavPath is not null)
            {
                TryDeleteBestEffort(tempWavPath);
            }

            if (encodedPath is not null)
            {
                TryDeleteBestEffort(encodedPath);
            }

            IsConverting = false;
        }
    }

    ///<summary>
    ///Opens the system file picker for any audio file or MP4 video, then probes the chosen file's size and duration
    ///so the Convert button and target-size bit-rate math have something to work from.
    ///</summary>
    [RelayCommand]
    private async Task PickAsync()
    {
        StatusMessage = null;
        Dictionary<DevicePlatform, IEnumerable<string>> fileTypes = new() { { DevicePlatform.Android, new[] { "audio/*", "video/mp4" } } };
        PickOptions options = new() { PickerTitle = "Choose a file to convert", FileTypes = new FilePickerFileType(fileTypes), };

        FileResult? result = await _filePicker.PickAsync(options);
        if (result is null)
        {
            return;
        }

        try
        {
            string sourcePath = await ResolveSourcePathAsync(result);
            TimeSpan duration = await _formatConverterService.ProbeDurationAsync(sourcePath);

            _sourcePath = sourcePath;
            _sourceDuration = duration;
            SourceFileName = result.FileName;
            SourceSummary = $"{FormatSize(new FileInfo(sourcePath).Length)} · {FormatDuration(duration)}";
            HasSource = true;
            Log_Picked(result.FileName);
            RecomputeBitRateFromTargetSize();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Log_PickFailed(ex);
            _sourcePath = null;
            HasSource = false;
            SourceFileName = null;
            SourceSummary = null;
            StatusMessage = "Couldn't read that file - it may not have an audio track.";
        }
    }

    ///<summary>
    ///Sets a preset bit rate, clearing any target-size request (the two are mutually exclusive ways of arriving at
    ///the same <see cref="BitRateBps"/>).
    ///</summary>
    private void SetBitRate(int bitRateBps)
    {
        TargetSizeText = string.Empty;
        BitRateBps = bitRateBps;
    }

    [RelayCommand]
    private void SetSmallerBitRate() => SetBitRate(96_000);

    [RelayCommand]
    private void SetSmallestBitRate() => SetBitRate(64_000);

    [RelayCommand]
    private void SetStandardBitRate() => SetBitRate(128_000);

    [RelayCommand]
    private void SetTargetFormat(ConversionTargetFormat format) => TargetFormat = format;

    partial void OnTargetFormatChanged(ConversionTargetFormat value) => ShowsCompressionControls = value != ConversionTargetFormat.Wav;
    #endregion

    #region Public properties
    ///<summary>
    ///The bit rate to encode at, in bits per second - either a preset picked directly, or computed from
    ///<see cref="TargetSizeText"/> and the picked file's duration.
    ///</summary>
    [ObservableProperty]
    public partial int BitRateBps { get; set; } = 128_000;

    ///<summary>
    ///Human-readable form of <see cref="BitRateBps"/> (e.g. "≈128 kbps") shown next to the bit-rate controls.
    ///</summary>
    [ObservableProperty]
    public partial string BitRateSummary { get; set; } = "≈128 kbps";

    ///<summary>
    ///How far through the current conversion it is, from 0 to 1. Meaningful while <see cref="IsConverting"/> is true.
    ///</summary>
    [ObservableProperty]
    public partial double ConvertFraction { get; set; }

    ///<summary>
    ///What the current conversion is doing, with its percentage. Meaningful while <see cref="IsConverting"/> is true.
    ///</summary>
    [ObservableProperty]
    public partial string ConvertStatus { get; set; } = string.Empty;

    ///<summary>
    ///True once a file has been picked and its duration is known, so the Convert button and bit-rate controls have
    ///something to act on.
    ///</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    public partial bool HasSource { get; set; }

    ///<summary>
    ///True from when the user taps Convert until the result is copied to shared storage or the conversion fails.
    ///</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    public partial bool IsConverting { get; set; }

    ///<summary>
    ///The picked file's name, or null if none has been picked yet.
    ///</summary>
    [ObservableProperty]
    public partial string? SourceFileName { get; set; }

    ///<summary>
    ///The picked file's size and duration, formatted for display (e.g. "84.8 MB · 1:12:40").
    ///</summary>
    [ObservableProperty]
    public partial string? SourceSummary { get; set; }

    ///<summary>
    ///Whether the bit-rate preset/target-size controls are shown - hidden for a <see cref="ConversionTargetFormat.Wav"/>
    ///target, which is lossless and so has no bit rate to choose.
    ///</summary>
    [ObservableProperty]
    public partial bool ShowsCompressionControls { get; set; } = true;

    ///<summary>
    ///What format to convert the picked file to.
    ///</summary>
    [ObservableProperty]
    public partial ConversionTargetFormat TargetFormat { get; set; } = ConversionTargetFormat.Aac;

    ///<summary>
    ///A target output size in megabytes (as typed), used to compute <see cref="BitRateBps"/> instead of picking a
    ///preset. Blank when a preset is in effect instead.
    ///</summary>
    [ObservableProperty]
    public partial string TargetSizeText { get; set; } = string.Empty;
    #endregion
}
