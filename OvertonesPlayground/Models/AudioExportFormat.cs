namespace OvertonesPlayground.Models;

///<summary>
///Compressed formats a library clip can be encoded to for sharing outside the app.
///</summary>
public enum AudioExportFormat
{
    ///<summary>AAC audio in ADTS framing (.aac). Supported on every Android device - AAC-LC encode is mandatory per
    ///the platform's compatibility requirements.</summary>
    Aac,

    ///<summary>MPEG-1 Layer III audio (.mp3). Best-effort: most Android devices have no MP3 <i>encoder</i>
    ///(decoders are common, encoders are not), so this frequently isn't available.</summary>
    Mp3,
}
