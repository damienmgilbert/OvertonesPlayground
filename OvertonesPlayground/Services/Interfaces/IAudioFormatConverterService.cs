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
    ///Encodes <paramref name="sourcePath"/> (a 16-bit PCM WAV file) into <paramref name="format"/> at
    ///<paramref name="bitRateBps"/> and returns the new file's path. <paramref name="progress"/> receives the
    ///fraction encoded (0 to 1), from a background thread.
    ///</summary>
    ///<exception cref="ArgumentException"><paramref name="sourcePath"/> or <paramref name="outputName"/> is null,
    ///empty, or whitespace.</exception>
    ///<exception cref="NotSupportedException">The platform has no encoder for <paramref name="format"/>, or
    ///<paramref name="sourcePath"/>'s sample rate isn't one AAC's ADTS framing supports.</exception>
    Task<string> ConvertFromWavAsync(string sourcePath, AudioExportFormat format, string outputName, int bitRateBps = 128_000, IProgress<double>? progress = null);

    ///<summary>
    ///Decodes <paramref name="sourcePath"/> into a new 16-bit PCM WAV file and returns its path. <paramref name="progress"/>
    ///receives the fraction decoded (0 to 1), from a background thread; it isn't called if the source's length is unknown.
    ///</summary>
    ///<exception cref="ArgumentException"><paramref name="sourcePath"/> or <paramref name="outputName"/> is null,
    ///empty, or whitespace.</exception>
    ///<exception cref="NotSupportedException">The platform has no decoder for <paramref name="sourcePath"/>'s
    ///format, or it carries no audio track.</exception>
    Task<string> ConvertToWavAsync(string sourcePath, string outputName, IProgress<double>? progress = null);

    ///<summary>
    ///Returns whether <paramref name="filePath"/> needs decoding before <c>WavFile</c> can read it, based on its
    ///extension.
    ///</summary>
    bool NeedsConversion(string filePath);

    ///<summary>
    ///Reads just <paramref name="sourcePath"/>'s container metadata to return its audio track's duration, without
    ///decoding it. Used to size a compression bit rate against a target file size before running a (possibly long)
    ///conversion.
    ///</summary>
    ///<exception cref="ArgumentException"><paramref name="sourcePath"/> is null, empty, or whitespace.</exception>
    ///<exception cref="NotSupportedException"><paramref name="sourcePath"/> carries no audio track.</exception>
    Task<TimeSpan> ProbeDurationAsync(string sourcePath);
    #endregion
}
