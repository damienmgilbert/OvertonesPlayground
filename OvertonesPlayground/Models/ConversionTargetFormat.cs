namespace OvertonesPlayground.Models;

///<summary>
///What the Convert page can turn a picked file into. Kept separate from <see cref="AudioExportFormat"/> (the
///Library page's export choices) so Library's existing export flow is unaffected by this page's extra options.
///</summary>
public enum ConversionTargetFormat
{
    ///<summary>Uncompressed 16-bit PCM WAV: decode (if the source isn't already WAV) and stop there. Lossless, but
    ///not useful for shrinking a file - use <see cref="Aac"/> or <see cref="Mp3"/> for that.</summary>
    Wav,

    ///<summary>AAC audio in ADTS framing (.aac). Supported on every Android device - AAC-LC encode is mandatory per
    ///the platform's compatibility requirements.</summary>
    Aac,

    ///<summary>MPEG-1 Layer III audio (.mp3). Best-effort: most Android devices have no MP3 <i>encoder</i>
    ///(decoders are common, encoders are not), so this frequently isn't available.</summary>
    Mp3,
}
