namespace OvertonesPlayground.Ontology.Facets;

///<summary>
///Coarse length buckets used for browsing.
///</summary>
public enum LengthClass
{
    ///<summary>
    ///Under a quarter of a second.
    ///</summary>
    Hit,

    ///<summary>
    ///Under one second.
    ///</summary>
    Short,

    ///<summary>
    ///One to four seconds.
    ///</summary>
    Medium,

    ///<summary>
    ///Four to fifteen seconds.
    ///</summary>
    Long,

    ///<summary>
    ///Fifteen seconds or more.
    ///</summary>
    Extended,
}
