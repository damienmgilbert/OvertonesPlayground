namespace OvertonesPlayground.Models;

///<summary>
///The step an audio import is currently working through.
///</summary>
public enum ImportStage
{
    ///<summary>
    ///Bringing the picked file into app storage.
    ///</summary>
    Copying,

    ///<summary>
    ///Decoding a compressed file (MP3, AAC, OGG, ...) into WAV.
    ///</summary>
    Decoding,

    ///<summary>
    ///Reading the clip's duration and saving it to the library.
    ///</summary>
    Finishing,
}
