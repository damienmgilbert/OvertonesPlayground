namespace OvertonesPlayground.Services.Interfaces;

///<summary>
///Non-destructive-by-convention edits over 16-bit PCM WAV files: every operation reads the source file and writes a
///brand new file, leaving the original untouched.
///</summary>
///<remarks>
///Every method validates its string arguments and throws <see cref="ArgumentException"/> if one is null, empty, or
///whitespace. Reading the source file can also throw <see cref="FileNotFoundException"/> (missing file),
///<see cref="InvalidDataException"/> (not a well-formed RIFF/WAV file), or <see cref="NotSupportedException"/> (not
///uncompressed 16-bit PCM).
///</remarks>
public interface IAudioEditorService
{
    #region Public methods

    ///<summary>
    ///Ramps the amplitude up at the start and down at the end over the given durations.
    ///</summary>
    Task<string> ApplyFadeAsync(string sourcePath, TimeSpan fadeIn, TimeSpan fadeOut, string outputName);

    ///<summary>
    ///Applies a constant gain, in decibels, to every sample.
    ///</summary>
    Task<string> ApplyGainAsync(string sourcePath, double gainDb, string outputName);

    ///<summary>
    ///Removes the [<paramref name="start"/>, <paramref name="end"/>] range from the middle, splicing what's before and
    ///after it back together.
    ///</summary>
    Task<string> CutAsync(string sourcePath, TimeSpan start, TimeSpan end, string outputName);

    ///<summary>
    ///Finds the sample nearest <paramref name="near"/> where the waveform crosses zero, searching up to
    ///<paramref name="maxSearch"/> in each direction. Returns <paramref name="near"/> unchanged if no crossing is
    ///found within range. Used to snap a trim handle so a cut doesn't land mid-waveform and click audibly.
    ///</summary>
    Task<TimeSpan> FindNearestZeroCrossingAsync(string sourcePath, TimeSpan near, TimeSpan maxSearch);

    ///<summary>
    ///Reads a WAV file and downsamples it into <paramref name="peakCount"/> amplitude peaks for waveform display.
    ///</summary>
    Task<float[]> GetWaveformPeaksAsync(string filePath, int peakCount);

    ///<summary>
    ///Scales the audio so its loudest sample reaches full scale.
    ///</summary>
    Task<string> NormalizeAsync(string sourcePath, string outputName);

    ///<summary>
    ///Plays the audio backwards.
    ///</summary>
    Task<string> ReverseAsync(string sourcePath, string outputName);

    ///<summary>
    ///Splits the audio at <paramref name="at"/> into two new files - everything before it, and everything from it
    ///on - and returns both paths.
    ///</summary>
    Task<(string BeforePath, string AfterPath)> SplitAsync(string sourcePath, TimeSpan at, string outputNameBefore, string outputNameAfter);

    ///<summary>
    ///Cuts the audio down to the [<paramref name="start"/>, <paramref name="end"/>] range.
    ///</summary>
    Task<string> TrimAsync(string sourcePath, TimeSpan start, TimeSpan end, string outputName);
    #endregion
}
