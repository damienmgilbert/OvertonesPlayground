namespace OvertonesPlayground.Ontology.Styles;

///<summary>
///What the fourth sequencer track plays in a style, after kick, backbeat and hats.
///</summary>
public enum StyleExtraPart
{
    ///<summary>Bass notes on the key's root, locked to the kick.</summary>
    Bass,

    ///<summary>Hand percussion (shaker, conga, cowbell ...).</summary>
    Percussion,

    ///<summary>A chord stab in the key.</summary>
    Chord,

    ///<summary>A cymbal: crash or ride.</summary>
    Cymbal,

    ///<summary>A tom.</summary>
    Tom,
}

///<summary>
///A range of whole numbers, inclusive.
///</summary>
public sealed record IntRange(int Min, int Max)
{
    ///<summary>Whether <paramref name="value"/> is in the range.</summary>
    public bool Contains(double value) => value >= Min && value <= Max;
}

///<summary>
///A ready-made setup of a style: the style's generator run with fixed choices, so it comes out the same every time.
///</summary>
///<param name="Id">Stable identifier, for example <c>house-909-deep</c>.</param>
///<param name="Name">Title to show.</param>
///<param name="Seed">Seed for the generator's random choices.</param>
///<param name="Tempo">Tempo in BPM.</param>
///<param name="Key">Key, such as <c>F minor</c> (see <see cref="Theory.Key.Parse"/>).</param>
///<param name="Kit">Kit the drums come from first.</param>
public sealed record StylePreset(string Id, string Name, int Seed, int Tempo, string Key, string Kit);

///<summary>
///What a musical style sounds like, as data a generator can follow: its tempo and swing, the scales and chord progressions it
///uses, the drum kits that suit it, and a library of patterns for each part. The styles live in <c>Data/styles.json</c>.
///</summary>
public sealed class StyleProfile
{
    #region Public properties
    ///<summary>Stable identifier, for example <c>boom-bap</c>.</summary>
    public string Key { get; set; } = string.Empty;

    ///<summary>Name to show.</summary>
    public string Name { get; set; } = string.Empty;

    ///<summary>Key of the style in the style taxonomy (<c>hip-hop</c>, <c>house</c> ...).</summary>
    public string Style { get; set; } = string.Empty;

    ///<summary>One line about the style, for the menu.</summary>
    public string Summary { get; set; } = string.Empty;

    ///<summary>Tempo range in BPM.</summary>
    public IntRange Tempo { get; set; } = new(90, 120);

    ///<summary>Swing range, in the Launchpad's eight levels (0 is straight).</summary>
    public IntRange Swing { get; set; } = new(0, 0);

    ///<summary>Keys of the scales the style uses (see <see cref="Scales"/>), the most typical first.</summary>
    public IReadOnlyList<string> Scales { get; set; } = ["minor"];

    ///<summary>Chord progressions in roman numerals, the most typical first.</summary>
    public IReadOnlyList<string> Progressions { get; set; } = [];

    ///<summary>Kits whose sound suits the style, the most typical first.</summary>
    public IReadOnlyList<string> Kits { get; set; } = [];

    ///<summary>The instrument that plays the backbeat: <c>snare</c>, <c>clap</c> (house, trap) or <c>rim</c> (a clave on the rim).</summary>
    public string BackbeatSound { get; set; } = "snare";

    ///<summary>Whether the style's bass is an 808 (so kicks must leave the sub-bass to it).</summary>
    public bool Uses808 { get; set; }

    ///<summary>What the fourth sequencer track plays.</summary>
    public StyleExtraPart Extra { get; set; } = StyleExtraPart.Bass;

    ///<summary>Hand percussion that suits the style, the most typical first.</summary>
    public IReadOnlyList<string> Percussion { get; set; } = ["shaker", "tambourine", "cowbell", "conga", "woodblock"];

    ///<summary>The share of the hat steps that play, the range that suits the style.</summary>
    public double MinHatDensity { get; set; }

    ///<summary>The share of the hat steps that play, the range that suits the style.</summary>
    public double MaxHatDensity { get; set; } = 1;

    ///<summary>Kick patterns, one character per step (see <see cref="StepPattern"/>).</summary>
    public IReadOnlyList<string> Kick { get; set; } = [];

    ///<summary>Backbeat (snare or clap) patterns.</summary>
    public IReadOnlyList<string> Backbeat { get; set; } = [];

    ///<summary>Hat patterns.</summary>
    public IReadOnlyList<string> Hats { get; set; } = [];

    ///<summary>Patterns for the fourth track (see <see cref="Extra"/>). If there are none, it follows the kick.</summary>
    public IReadOnlyList<string> ExtraPatterns { get; set; } = [];

    ///<summary>Ready-made setups of this style.</summary>
    public IReadOnlyList<StylePreset> Presets { get; set; } = [];

    ///<summary>The scales, looked up (any unknown key is skipped).</summary>
    public IReadOnlyList<Scale> ScaleList => [.. Scales.Select(Theory.Scales.Find).OfType<Scale>()];

    ///<summary>The progressions, parsed.</summary>
    public IReadOnlyList<Progression> ProgressionList => [.. Progressions.Select(Progression.Parse)];
    #endregion
}

///<summary>
///Shape of <c>styles.json</c>.
///</summary>
internal sealed class StyleProfileFile
{
    public List<StyleProfile> Profiles { get; set; } = [];
}
