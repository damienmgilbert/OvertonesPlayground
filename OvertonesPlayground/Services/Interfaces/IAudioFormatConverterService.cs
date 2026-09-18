using OvertonesPlayground.Models;

namespace OvertonesPlayground.Services.Interfaces;

///<summary>
///Converts between 16-bit PCM WAV and compressed audio formats using the platform's native codecs: decoding
///arbitrary formats (MP3, AAC/M4A, OGG, etc.) into WAV so the rest of the app's WAV-only pipeline (<c>WavFile</c>,
///trim/effects, waveform display) can read them, and encoding WAV back out to AAC/MP3 for sharing.
///</summary>
public interface IAudioFormatConverterService
{
    #region Public methods
    ///<summary>
    ///Encodes <paramref name="sourcePath"/> (a 16-bit PCM WAV file) into <paramref name="format"/> and returns the
    ///new file's path.
    ///</summary>
    ///<exception cref="ArgumentException"><paramref name="sourcePath"/> or <paramref name="outputName"/> is null,
    ///empty, or whitespace.</exception>
    ///<exception cref="NotSupportedException">The platform has no encoder for <paramref name="format"/>, or
    ///<paramref name="sourcePath"/>'s sample rate isn't one AAC's ADTS framing supports.</exception>
    Task<string> ConvertFromWavAsync(string sourcePath, AudioExportFormat format, string outputName);

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
