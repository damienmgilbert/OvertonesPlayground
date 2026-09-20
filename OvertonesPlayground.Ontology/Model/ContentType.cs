namespace OvertonesPlayground.Ontology.Model;

///<summary>
///Structural role of a sample in a production, independent of what instrument it is.
///</summary>
public enum ContentType
{
    ///<summary>
    ///Not determined.
    ///</summary>
    Unknown,

    ///<summary>
    ///A single hit or note.
    ///</summary>
    OneShot,

    ///<summary>
    ///A repeating musical phrase that loops seamlessly.
    ///</summary>
    Loop,

    ///<summary>
    ///A drum break or chopped groove.
    ///</summary>
    Break,

    ///<summary>
    ///A non-repeating musical phrase or vocal line.
    ///</summary>
    Phrase,

    ///<summary>
    ///A short chord or orchestra hit.
    ///</summary>
    Stab,

    ///<summary>
    ///A sustained bed: atmosphere, ambience, noise, pad.
    ///</summary>
    Texture,

    ///<summary>
    ///A riser, sweep, swell, impact or other transition effect.
    ///</summary>
    Transition,
}
