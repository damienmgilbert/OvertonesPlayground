namespace OvertonesPlayground.Services.Implementations;

///<summary>
///Shared "write a derived WAV file into an app-private output folder" helper, used by every service that produces a
///new audio file from existing ones (edits, mixdowns, generated audio).
///</summary>
internal static class DerivedAudioFileWriter
{
    #region Private methods
    ///<summary>
    ///Replaces characters that aren't valid in a file name (e.g. a clip named with a "/" or ":") with "_", so an
    ///arbitrary clip/operation name can never produce an invalid or unexpectedly-nested output path.
    ///</summary>
    private static string SanitizeFileNameSegment(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(c => invalid.Contains(c) ? '_' : c));
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Writes <paramref name="wav"/> into <paramref name="directory"/> (created if missing) as a timestamped,
    ///filename-sanitized WAV file named after <paramref name="outputName"/>, and returns its path.
    ///</summary>
    public static async Task<string> SaveAsync(WavFile wav, string directory, string outputName)
    {
        Directory.CreateDirectory(directory);

        string fileName = $"{SanitizeFileNameSegment(outputName)}_{DateTime.Now:yyyyMMdd_HHmmss}.wav";
        string path = Path.Combine(directory, fileName);
        await wav.WriteAsync(path);
        return path;
    }
    #endregion
}
