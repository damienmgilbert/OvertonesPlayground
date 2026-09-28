using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

///<summary>
///View model for the Video Converter page: picks a video file (MP4), extracts its audio track, and saves it to the
///shared Music folder as WAV or MP3.
///</summary>
public partial class VideoConverterViewModel : BaseViewModel
{
    #region Fields
    private readonly IVideoConverterService _videoConverterService;
    #endregion

    #region Constructors
    public VideoConverterViewModel(IVideoConverterService videoConverterService, ILogger<VideoConverterViewModel> logger) : base(logger)
    {
        _videoConverterService = videoConverterService;
        Title = "Video Converter";
    }
    #endregion

    #region Private methods
    ///<summary>
    ///Picks a video and converts its audio track, showing progress while it does. The command can't be started again
    ///while it is running.
    ///</summary>
    [RelayCommand]
    private async Task ConvertAsync()
    {
        if (IsConverting)
        {
            return;
        }

        Log_ConvertingVideo(SelectedFormat);
        StatusMessage = null;
        ConversionFraction = 0;
        ConversionStatus = "Preparing the file...";
        IsConverting = true;
        try
        {
            Progress<ImportProgress> progress = new(OnConversionProgress);
            VideoConversionOutcome? outcome = await _videoConverterService.ConvertFromPickerAsync(SelectedFormat, progress);
            if (outcome is not null)
            {
                Log_ConvertedVideo(outcome.Value.FileName);
                StatusMessage = outcome.Value.PublicLocation is not null
                    ? $"Saved '{outcome.Value.FileName}' to {outcome.Value.PublicLocation}."
                    : $"Converted '{outcome.Value.FileName}', but couldn't save it to shared storage.";
            }
            else
            {
                Log_ConversionCanceled();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or OutOfMemoryException)
        {
            Log_ConversionFailed(ex);
            StatusMessage = SelectedFormat == VideoConversionFormat.Mp3
                ? "Couldn't convert that file to MP3 - many Android devices don't have an MP3 encoder. Try WAV instead."
                : "Couldn't convert that file. Make sure it's a video with an audio track.";
        }
        finally
        {
            IsConverting = false;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Converting a video to {Format}.")]
    private partial void Log_ConvertingVideo(VideoConversionFormat format);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Converted video to '{FileName}'.")]
    private partial void Log_ConvertedVideo(string fileName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Video conversion canceled.")]
    private partial void Log_ConversionCanceled();

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to convert a video.")]
    private partial void Log_ConversionFailed(Exception exception);

    ///<summary>
    ///Shows a conversion's progress. Reports arrive on the UI thread but slightly after they are made, so one can land
    ///once the conversion is over; that one is ignored.
    ///</summary>
    private void OnConversionProgress(ImportProgress progress)
    {
        if (!IsConverting)
        {
            return;
        }

        string stage = progress.Stage switch
        {
            ImportStage.Copying => "Copying the file...",
            ImportStage.Decoding => "Extracting the audio track...",
            ImportStage.Encoding => "Encoding to MP3...",
            ImportStage.Finishing => "Saving...",
            _ => string.Empty,
        };

        ConversionFraction = progress.Fraction;
        ConversionStatus = $"{stage} {progress.Fraction:P0}";
    }
    #endregion

    #region Public properties
    ///<summary>
    ///How far through the current conversion it is, from 0 to 1. Meaningful while <see cref="IsConverting"/> is true.
    ///</summary>
    [ObservableProperty]
    public partial double ConversionFraction { get; set; }

    ///<summary>
    ///What the current conversion is doing, with its percentage. Meaningful while <see cref="IsConverting"/> is true.
    ///</summary>
    [ObservableProperty]
    public partial string ConversionStatus { get; set; } = string.Empty;

    ///<summary>
    ///Every output format the picker offers.
    ///</summary>
    public IReadOnlyList<VideoConversionFormat> Formats { get; } = Enum.GetValues<VideoConversionFormat>();

    ///<summary>
    ///True from when the user taps Convert until the file is saved, the picker is canceled, or the conversion fails.
    ///</summary>
    [ObservableProperty]
    public partial bool IsConverting { get; set; }

    ///<summary>
    ///The format the extracted audio track is converted to.
    ///</summary>
    [ObservableProperty]
    public partial VideoConversionFormat SelectedFormat { get; set; }
    #endregion
}
