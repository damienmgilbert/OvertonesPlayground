namespace OvertonesPlayground.Ontology.Model;

///<summary>
///Everything the ontology says a sample <em>is</em>: instrument, kit, style, content type and origin, each with
///confidence and evidence, plus the attributes parsed from its name.
///</summary>
///<param name="Instrument">Primary instrument concept key (see <see cref="Taxonomies.Instruments"/>).</param>
///<param name="AlternateInstruments">Other plausible instruments, for example the second word of <c>Snare Clap</c>.</param>
///<param name="ContentType">Structural role.</param>
///<param name="Origin">How the sound was produced.</param>
///<param name="Kit">Kit or machine concept key (see <see cref="Taxonomies.Kits"/>), or null.</param>
///<param name="Style">Music style concept key (see <see cref="Taxonomies.Styles"/>), or null.</param>
///<param name="Attributes">Tempo, key, variation number and stem parsed from the name.</param>
///<param name="NeedsReview">Evidence sources disagree or confidence is low; a human should look.</param>
///<param name="ReviewReasons">Why <paramref name="NeedsReview"/> is set.</param>
public sealed record SampleClassification(
    Label<string> Instrument,
    IReadOnlyList<Label<string>> AlternateInstruments,
    Label<ContentType> ContentType,
    Label<SoundOrigin> Origin,
    Label<string>? Kit,
    Label<string>? Style,
    NamedAttributes Attributes,
    bool NeedsReview,
    IReadOnlyList<string> ReviewReasons)
{
    #region Public methods
    ///<summary>
    ///An empty classification used between analysis and classification: nothing is known yet except the name stem.
    ///</summary>
    public static SampleClassification Placeholder(string stem) =>
        new(
            new Label<string>("unclassified", 0.0, []),
            [],
            new Label<ContentType>(OvertonesPlayground.Ontology.Model.ContentType.Unknown, 0.0, []),
            new Label<SoundOrigin>(SoundOrigin.Unknown, 0.0, []),
            null,
            null,
            NamedAttributes.Empty(stem),
            false,
            []);
    #endregion
}
