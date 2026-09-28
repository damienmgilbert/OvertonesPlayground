namespace OvertonesPlayground.Models;

///<summary>
///The step an audio import or video-to-audio conversion is currently working through.
///</summary>
public enum ImportStage
{
    ///<summary>
    ///Bringing the picked file into app storage.
    ///</summary>
    Copying,

    ///<summary>
    ///Decoding a compressed file (MP3, AAC, OGG, ...) or a video's audio track into WAV.
    ///</summary>
    Decoding,

    ///<summary>
    ///Encoding a decoded WAV into a compressed output format (e.g. converting a video's audio to MP3).
    ///</summary>
    Encoding,

    ///<summary>
    ///Reading the clip's duration and saving it to the library, or saving a converted file to shared storage.
    ///</summary>
    Finishing,
}
