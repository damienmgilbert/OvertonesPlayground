using OvertonesPlayground.Models;

namespace OvertonesPlayground.Services.Interfaces;

///<summary>
///Extracts the audio track from a video file (e.g. an MP4) and saves it to the shared Music folder as WAV or MP3.
///</summary>
public interface IVideoConverterService
{
    #region Public methods
    ///<summary>
    ///Opens the system file picker for a video file, extracts its audio track, converts it to <paramref name="format"/>,
    ///and exports the result to the shared Music folder. Returns null if the user cancels the picker.
    ///</summary>
    ///<param name="format">The audio format to produce.</param>
    ///<param name="progress">Told how far along the conversion is once a file has been picked.</param>
    ///<exception cref="NotSupportedException">The picked file has no audio track, or the platform has no decoder or
    ///encoder for it.</exception>
    Task<VideoConversionOutcome?> ConvertFromPickerAsync(VideoConversionFormat format, IProgress<ImportProgress>? progress = null);
    #endregion
}
