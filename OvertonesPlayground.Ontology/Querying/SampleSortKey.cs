namespace OvertonesPlayground.Ontology.Querying;

///<summary>
///What to order results by.
///</summary>
public enum SampleSortKey
{
    ///<summary>
    ///Alphabetical by name.
    ///</summary>
    Name,

    ///<summary>
    ///By instrument category, family and name.
    ///</summary>
    Instrument,

    ///<summary>
    ///By length.
    ///</summary>
    Duration,

    ///<summary>
    ///By integrated loudness.
    ///</summary>
    Loudness,

    ///<summary>
    ///By spectral centroid.
    ///</summary>
    Brightness,

    ///<summary>
    ///By attack time.
    ///</summary>
    Attack,

    ///<summary>
    ///By effective tempo.
    ///</summary>
    Tempo,

    ///<summary>
    ///By pitch (key in the name or detected fundamental).
    ///</summary>
    Pitch,
}
