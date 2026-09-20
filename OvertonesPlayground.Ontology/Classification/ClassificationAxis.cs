namespace OvertonesPlayground.Ontology.Classification;

///<summary>
///The dimensions along which a sample is classified.
///</summary>
public enum ClassificationAxis
{
    ///<summary>What sound source it is (an instrument taxonomy concept key).</summary>
    Instrument,

    ///<summary>Which kit, machine or pack it comes from (a kit taxonomy concept key).</summary>
    Kit,

    ///<summary>The music style (a style taxonomy concept key).</summary>
    Style,

    ///<summary>Its structural role (a <see cref="Model.ContentType"/> name).</summary>
    ContentType,

    ///<summary>How it was produced (a <see cref="Model.SoundOrigin"/> name).</summary>
    Origin,
}
