namespace OvertonesPlayground.Services.Interfaces;

/// <summary>
/// Non-destructive-by-convention edits over 16-bit PCM WAV files: every operation reads
/// the source file and writes a brand new file, leaving the original untouched.
/// </summary>
public interface IAudioEditorService
{
    Task<float[]> GetWaveformPeaksAsync(string filePath, int peakCount);

    Task<string> TrimAsync(string sourcePath, TimeSpan start, TimeSpan end, string outputName);

    Task<string> ApplyGainAsync(string sourcePath, double gainDb, string outputName);

    Task<string> ApplyFadeAsync(string sourcePath, TimeSpan fadeIn, TimeSpan fadeOut, string outputName);

    Task<string> ReverseAsync(string sourcePath, string outputName);

    Task<string> NormalizeAsync(string sourcePath, string outputName);
}
