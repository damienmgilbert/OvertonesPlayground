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
    ///Reduces dynamic range: while the signal's smoothed envelope exceeds <paramref name="thresholdDb"/>, gain above
    ///that threshold is scaled down by <paramref name="ratio"/>, with <paramref name="attackMs"/>/<paramref name="releaseMs"/>
    ///controlling how fast the envelope follows rising/falling level.
    ///</summary>
    Task<string> ApplyCompressionAsync(string sourcePath, double thresholdDb, double ratio, double attackMs, double releaseMs, string outputName);

    ///<summary>
    ///Runs a 3-band equalizer (low shelf / mid peaking / high shelf) over the audio, boosting or cutting each band by
    ///its gain in decibels.
    ///</summary>
    Task<string> ApplyEqualizerAsync(string sourcePath, double lowGainDb, double midGainDb, double highGainDb, string outputName);

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
    ///Splices <paramref name="insertPath"/>'s audio into <paramref name="sourcePath"/> at <paramref name="at"/>,
    ///resampling/channel-converting the inserted clip to match the source first if they differ, and returns the new
    ///file's path.
    ///</summary>
    ///<exception cref="NotSupportedException"><paramref name="insertPath"/>'s channel count can't be converted to
    ///match <paramref name="sourcePath"/> (only mono/stereo conversion is supported).</exception>
    Task<string> InsertAsync(string sourcePath, string insertPath, TimeSpan at, string outputName);

    ///<summary>
    ///Scales the audio so its loudest sample reaches full scale.
    ///</summary>
    Task<string> NormalizeAsync(string sourcePath, string outputName);

    ///<summary>
    ///Reduces steady-state noise (hum, hiss, rumble) via spectral subtraction, estimating the noise profile from the
    ///first <paramref name="noiseSampleDuration"/> of the clip - trim to a stretch of noise-only audio first for the
    ///best result.
    ///</summary>
    Task<string> ReduceNoiseAsync(string sourcePath, TimeSpan noiseSampleDuration, string outputName);

    ///<summary>
    ///Cancels out audio that's identical in both channels via phase cancellation (<c>output = left - right</c>),
    ///which removes a centered vocal from a true-stereo mix. Quality depends entirely on the source actually having a
    ///centered, unpanned vocal.
    ///</summary>
    ///<exception cref="NotSupportedException"><paramref name="sourcePath"/> isn't stereo.</exception>
    Task<string> RemoveVocalsAsync(string sourcePath, string outputName);

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
