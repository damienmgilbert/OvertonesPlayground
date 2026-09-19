namespace OvertonesPlayground.Services.Implementations;

///<summary>
///Shared "write a derived WAV file into an app-private output folder" helper, used by every service that produces a
///new audio file from existing ones (edits, mixdowns, generated audio).
///</summary>
internal static class DerivedAudioFileWriter
{
    #region Private methods
    ///<summary>
    ///Builds a timestamped, filename-sanitized path for <paramref name="outputName"/> inside <paramref name="directory"/>
    ///(created if missing). The path is never one that already exists.
    ///</summary>
    private static string BuildPath(string directory, string outputName, string extension)
    {
        Directory.CreateDirectory(directory);
        string stem = $"{SanitizeFileNameSegment(outputName)}_{DateTime.Now:yyyyMMdd_HHmmss}";
        string path = Path.Combine(directory, $"{stem}.{extension}");

        // The time is only to the second, so the same edit made twice in a row would name the same file, and the first result may
        // already be a clip in the library that would then play the second one's audio.
        for (int copy = 2; File.Exists(path); copy++)
        {
            path = Path.Combine(directory, $"{stem}_{copy}.{extension}");
        }

        return path;
    }

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
    ///Writes raw encoded bytes (e.g. AAC/MP3) into <paramref name="directory"/> (created if missing) as a
    ///timestamped, filename-sanitized file named after <paramref name="outputName"/>, and returns its path.
    ///</summary>
    public static async Task<string> SaveBytesAsync(byte[] bytes, string directory, string outputName, string extension)
    {
        string path = BuildPath(directory, outputName, extension);
        await File.WriteAllBytesAsync(path, bytes);
        return path;
    }

    ///<summary>
    ///Writes <paramref name="wav"/> into <paramref name="directory"/> (created if missing) as a timestamped,
    ///filename-sanitized WAV file named after <paramref name="outputName"/>, and returns its path.
    ///</summary>
    public static async Task<string> SaveAsync(WavFile wav, string directory, string outputName)
    {
        string path = BuildPath(directory, outputName, "wav");
        await wav.WriteAsync(path);
        return path;
    }
    #endregion
}
