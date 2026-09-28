namespace OvertonesPlayground.Models;

///<summary>
///Output format for a video's extracted audio track.
///</summary>
public enum VideoConversionFormat
{
    ///<summary>16-bit PCM WAV - uncompressed, and guaranteed to work on any device.</summary>
    Wav,

    ///<summary>MPEG-1 Layer III audio (.mp3). Best-effort, like <see cref="AudioExportFormat.Mp3"/>: most Android
    ///devices have no MP3 <i>encoder</i>, so this frequently isn't available.</summary>
    Mp3,
}
