namespace OvertonesPlayground.Ontology.Model;

///<summary>
///How a sound was originally produced.
///</summary>
public enum SoundOrigin
{
    ///<summary>
    ///Not determined.
    ///</summary>
    Unknown,

    ///<summary>
    ///A real acoustic instrument or voice.
    ///</summary>
    Acoustic,

    ///<summary>
    ///An analog synthesizer or analog drum machine.
    ///</summary>
    Analog,

    ///<summary>
    ///A digital synthesizer or digital sound source.
    ///</summary>
    Digital,

    ///<summary>
    ///A classic drum machine or sampler workstation.
    ///</summary>
    DrumMachine,

    ///<summary>
    ///Sampled from vinyl or another recorded source.
    ///</summary>
    Sampled,

    ///<summary>
    ///Heavily processed or effected.
    ///</summary>
    Processed,
}
