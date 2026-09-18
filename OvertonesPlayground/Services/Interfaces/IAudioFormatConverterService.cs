namespace OvertonesPlayground.Services.Interfaces;

///<summary>
///Decodes arbitrary audio formats (MP3, AAC/M4A, OGG, etc.) into 16-bit PCM WAV using the platform's native codecs,
///so files the rest of the app's WAV-only pipeline (<c>WavFile</c>, trim/effects, waveform display) can't read
///directly can still be imported.
///</summary>
public interface IAudioFormatConverterService
{
    #region Public methods
    ///<summary>
    ///Decodes <paramref name="sourcePath"/> into a new 16-bit PCM WAV file and returns its path.
    ///</summary>
    ///<exception cref="ArgumentException"><paramref name="sourcePath"/> or <paramref name="outputName"/> is null,
    ///empty, or whitespace.</exception>
    ///<exception cref="NotSupportedException">The platform has no decoder for <paramref name="sourcePath"/>'s
    ///format, or it carries no audio track.</exception>
    Task<string> ConvertToWavAsync(string sourcePath, string outputName);

    ///<summary>
    ///Returns whether <paramref name="filePath"/> needs decoding before <c>WavFile</c> can read it, based on its
    ///extension.
    ///</summary>
    bool NeedsConversion(string filePath);
    #endregion
}
