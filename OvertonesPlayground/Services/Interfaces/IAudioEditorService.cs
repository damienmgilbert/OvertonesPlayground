namespace OvertonesPlayground.Services.Interfaces;

/// <summary>
/// Non-destructive-by-convention edits over 16-bit PCM WAV files: every operation reads
/// the source file and writes a brand new file, leaving the original untouched.
/// </summary>
public interface IAudioEditorService
{
    /// <summary>Reads a WAV file and downsamples it into <paramref name="peakCount"/> amplitude peaks for waveform display.</summary>
    Task<float[]> GetWaveformPeaksAsync(string filePath, int peakCount);

    /// <summary>Cuts the audio down to the [<paramref name="start"/>, <paramref name="end"/>] range.</summary>
    Task<string> TrimAsync(string sourcePath, TimeSpan start, TimeSpan end, string outputName);

    /// <summary>Applies a constant gain, in decibels, to every sample.</summary>
    Task<string> ApplyGainAsync(string sourcePath, double gainDb, string outputName);

    /// <summary>Ramps the amplitude up at the start and down at the end over the given durations.</summary>
    Task<string> ApplyFadeAsync(string sourcePath, TimeSpan fadeIn, TimeSpan fadeOut, string outputName);

    /// <summary>Plays the audio backwards.</summary>
    Task<string> ReverseAsync(string sourcePath, string outputName);

    /// <summary>Scales the audio so its loudest sample reaches full scale.</summary>
    Task<string> NormalizeAsync(string sourcePath, string outputName);
}
