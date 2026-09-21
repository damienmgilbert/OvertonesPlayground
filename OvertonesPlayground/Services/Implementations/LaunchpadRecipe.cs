using OvertonesPlayground.Models;
using OvertonesPlayground.Ontology.Model;
using OvertonesPlayground.Ontology.Querying;

namespace OvertonesPlayground.Services.Implementations;

///<summary>
///Sounds for one lane of one bank, from the bottom row up.
///</summary>
///<param name="Bank">The bank, 0 to 3.</param>
///<param name="Lane">The column (see <see cref="LaunchpadExampleBuilder"/>'s lanes).</param>
///<param name="Sounds">Sample ids and the level each is brought to, bottom row first.</param>
///<param name="Loop">Whether the pads loop.</param>
internal sealed record RecipeFill(int Bank, int Lane, IReadOnlyList<(string SampleId, ExampleLevel Level)> Sounds, bool Loop = false);

///<summary>
///A lane's column mixer settings.
///</summary>
internal sealed record RecipeMix(int Lane, double Pan = 0, double Send = 0, double Volume = 1);

///<summary>
///Which pad a sequencer track takes its sample from.
///</summary>
internal sealed record RecipeTrack(int Track, int Bank, int Lane, int Row = LaunchpadExampleBuilder.PrimaryRow);

///<summary>
///A sequencer pattern: one step string per track (see <see cref="LaunchpadExampleBuilder.Pattern"/>).
///</summary>
internal sealed record RecipePattern(int Index, IReadOnlyList<(int Track, string Steps)> Lines);

///<summary>
///A whole Launchpad setup written as data: which sample goes on which pad, the mixer, the sequencer's tracks and patterns, and
///what key and style it is in. The generator writes recipes, the quality checks read them, and <see cref="Build"/> turns one into a
///project through <see cref="LaunchpadExampleBuilder"/>, the same way the hand-written examples are built, so a recipe's pads get the
///same loudness matching and lane colours.
///</summary>
internal sealed record LaunchpadRecipe
{
    #region Public properties
    ///<summary>Title with the style, tempo and key.</summary>
    public string Title { get; init; } = string.Empty;

    ///<summary>What is on each bank and in the sequencer.</summary>
    public string Description { get; init; } = string.Empty;

    ///<summary>The style profile it was made from.</summary>
    public string StyleKey { get; init; } = string.Empty;

    ///<summary>The seed it was generated with.</summary>
    public int Seed { get; init; }

    ///<summary>Tempo in BPM.</summary>
    public int Tempo { get; init; }

    ///<summary>Swing, as one of the Launchpad's eight levels.</summary>
    public int SwingLevel { get; init; }

    ///<summary>The key's home note, 0 to 11.</summary>
    public int RootPitchClass { get; init; }

    ///<summary>The key's scale, as a position in <see cref="LaunchpadScale"/>.</summary>
    public int ScaleIndex { get; init; }

    ///<summary>The kit the drums come from first.</summary>
    public string Kit { get; init; } = string.Empty;

    ///<summary>The pads.</summary>
    public IReadOnlyList<RecipeFill> Fills { get; init; } = [];

    ///<summary>The column mixer.</summary>
    public IReadOnlyList<RecipeMix> Mixes { get; init; } = [];

    ///<summary>The sequencer's tracks.</summary>
    public IReadOnlyList<RecipeTrack> Tracks { get; init; } = [];

    ///<summary>The sequencer's patterns.</summary>
    public IReadOnlyList<RecipePattern> Patterns { get; init; } = [];

    ///<summary>The key.</summary>
    public Ontology.Theory.Key Key => new(new Ontology.Theory.PitchClass(RootPitchClass), LaunchpadScale.Of(ScaleIndex));
    #endregion

    #region Public methods
    ///<summary>
    ///The sounds of one lane of one bank, bottom row first, or none.
    ///</summary>
    public IReadOnlyList<string> SoundsAt(int bank, int lane) =>
        [.. Fills.Where(fill => fill.Bank == bank && fill.Lane == lane).SelectMany(fill => fill.Sounds.Select(sound => sound.SampleId))];

    ///<summary>
    ///Assembles the recipe with a builder, so tests and checks can see where each sample went.
    ///</summary>
    ///<exception cref="InvalidOperationException">A sample of the recipe is not in the bank.</exception>
    public LaunchpadExampleBuilder Assemble(SampleIndex index, Func<Sample, string> pathOf)
    {
        LaunchpadExampleBuilder builder = new(index, pathOf, Tempo, SwingLevel, ScaleIndex);
        foreach (RecipeFill fill in Fills)
        {
            IEnumerable<(Sample, ExampleLevel)> items = fill.Sounds.Select(sound => (index.Find(sound.SampleId) ?? throw new InvalidOperationException($"The sound bank has no sample '{sound.SampleId}'."), sound.Level));
            builder.Fill(fill.Bank, fill.Lane, items, fill.Loop);
        }

        foreach (RecipeMix mix in Mixes)
        {
            builder.Mix(mix.Lane, mix.Pan, mix.Send, mix.Volume);
        }

        foreach (RecipeTrack track in Tracks)
        {
            builder.Track(track.Track, track.Bank, track.Lane, track.Row);
        }

        foreach (RecipePattern pattern in Patterns)
        {
            builder.Pattern(pattern.Index, [.. pattern.Lines]);
        }

        LaunchpadProject project = builder.Project;
        project.RootPitchClass = RootPitchClass;
        project.StyleKey = StyleKey;
        project.Seed = Seed;
        project.Origin = LaunchpadProjectOrigin.Generated;
        return builder;
    }

    ///<summary>
    ///Builds the project, giving every pad and track the path <paramref name="pathOf"/> returns for its sample.
    ///</summary>
    ///<exception cref="InvalidOperationException">A sample of the recipe is not in the bank.</exception>
    public LaunchpadProject Build(SampleIndex index, Func<Sample, string> pathOf) => Assemble(index, pathOf).Project;
    #endregion
}
