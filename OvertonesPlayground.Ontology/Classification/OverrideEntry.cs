namespace OvertonesPlayground.Ontology.Classification;

///<summary>
///A hand-written correction for one file. Any axis left null keeps the automatic result.
///</summary>
public sealed class OverrideEntry
{
    ///<summary>Content type name (<see cref="Model.ContentType"/>).</summary>
    public string? ContentType { get; set; }

    ///<summary>File name with extension.</summary>
    public string File { get; set; } = string.Empty;

    ///<summary>Instrument concept key.</summary>
    public string? Instrument { get; set; }

    ///<summary>Kit concept key.</summary>
    public string? Kit { get; set; }

    ///<summary>Why the override exists.</summary>
    public string? Note { get; set; }

    ///<summary>Sound origin name (<see cref="SoundOrigin"/>).</summary>
    public string? Origin { get; set; }

    ///<summary>Style concept key.</summary>
    public string? Style { get; set; }
}
