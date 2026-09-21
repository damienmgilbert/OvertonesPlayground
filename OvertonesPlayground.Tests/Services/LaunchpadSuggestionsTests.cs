using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Tests.Services;

///<summary>
///Suggestions for the pads of a real setup: the boom-bap example, built against the real sound bank.
///</summary>
public sealed class LaunchpadSuggestionsTests
{
    #region Constants
    private const int KitBank = 0;
    #endregion

    #region Private methods
    private static LaunchpadProject BoomBap() => LaunchpadExamples.Build(LaunchpadExamples.BoomBapId, RealCatalog.Index, sample => "/data/LaunchpadSamples/" + sample.Id);

    private static Sample SampleNamed(string name) => RealCatalog.Index.Find(name + ".wav")!;

    private static bool IsA(Sample sample, string key) => Taxonomies.Instruments.Get(sample.Classification.Instrument.Value).IsA(key);

    private static int Pad(int row, int column) => (row * LaunchpadProject.ColumnCount) + column;
    #endregion

    #region Public methods
    [Fact]
    public void SampleOf_FindsTheSoundBankSampleFromTheFileNameEvenForACopy()
    {
        LaunchpadPad pad = new() { ClipPath = "/data/Clips/Mallet Guitar C3 (2).wav", Label = "whatever" };
        LaunchpadPad byLabel = new() { ClipPath = "/data/Clips/renamed.wav", Label = "Mallet Guitar E2" };

        Assert.Equal("Mallet Guitar C3.wav", LaunchpadSuggestions.SampleOf(pad, RealCatalog.Index)?.Id);
        Assert.Equal("Mallet Guitar E2.wav", LaunchpadSuggestions.SampleOf(byLabel, RealCatalog.Index)?.Id);
        Assert.Null(LaunchpadSuggestions.SampleOf(new LaunchpadPad { ClipPath = "/x/My recording.wav", Label = "My recording" }, RealCatalog.Index));
    }

    [Fact]
    public void KeyOf_AProjectWithoutAKey_IsInferredFromItsPitchedSounds()
    {
        LaunchpadProject project = BoomBap();
        project.RootPitchClass = null;

        Assert.NotNull(LaunchpadSuggestions.KeyOf(project, RealCatalog.Index));

        project.RootPitchClass = 4;
        Assert.Equal("E minor", LaunchpadSuggestions.KeyOf(project, RealCatalog.Index)!.Name);
    }

    [Fact]
    public void Suggest_AnEmptyPadInTheHatsColumn_OffersHatsNotAlreadyOnThePads()
    {
        LaunchpadProject project = BoomBap();
        int hats = LaunchpadExampleBuilder.Hats;
        project.Pads.RemoveAll(pad => pad.Bank == KitBank && pad.Index == Pad(0, hats));

        IReadOnlyList<LaunchpadSuggestion> suggestions = LaunchpadSuggestions.Suggest(project, KitBank, Pad(0, hats), 6, RealCatalog.Index);

        Assert.NotEmpty(suggestions);
        Assert.All(suggestions, s => Assert.True(IsA(SampleNamed(s.SampleName), "hihat"), s.SampleName));
        HashSet<string> used = [.. project.Pads.Select(pad => pad.Label)];
        Assert.DoesNotContain(suggestions, s => used.Contains(s.SampleName));
    }

    [Fact]
    public void Suggest_APadWithASound_OffersAlternativesOfTheSameFamily()
    {
        LaunchpadProject project = BoomBap();
        LaunchpadPad snare = project.Pads.First(pad => pad.Bank == KitBank && pad.Index == Pad(7, LaunchpadExampleBuilder.Backbeat));

        IReadOnlyList<LaunchpadSuggestion> suggestions = LaunchpadSuggestions.Suggest(project, KitBank, snare.Index, 5, RealCatalog.Index);

        Assert.NotEmpty(suggestions);
        Assert.All(suggestions, s => Assert.True(IsA(SampleNamed(s.SampleName), "snare"), s.SampleName));
        Assert.All(suggestions, s => Assert.NotEqual(snare.Label, s.SampleName));
    }

    [Fact]
    public void Suggest_ForTheBoomBapProject_KeepsToEMinorAndNinetyBpm()
    {
        LaunchpadProject project = BoomBap();
        project.Pads.RemoveAll(pad => pad.Bank == 1 && pad.Index % 8 == LaunchpadExampleBuilder.Colour);

        IReadOnlyList<LaunchpadSuggestion> suggestions = LaunchpadSuggestions.Suggest(project, 1, Pad(7, LaunchpadExampleBuilder.Colour), 8, RealCatalog.Index);
        OvertonesPlayground.Ontology.Theory.Key key = OvertonesPlayground.Ontology.Theory.Key.Parse("E minor");

        Assert.All(suggestions, s => Assert.True(OvertonesPlayground.Ontology.Theory.KeyCompatibility.CanPlayIn(SampleNamed(s.SampleName), key), s.SampleName));
        Assert.All(suggestions.Select(s => SampleNamed(s.SampleName)).Where(OvertonesPlayground.Ontology.Theory.KeyCompatibility.IsPassage), loop => Assert.True(LaunchpadExampleBuilder.RunsAt(loop, 90), loop.Name));
    }

    [Fact]
    public void FillColumn_FillsEveryEmptyPadWithDifferentSoundsBottomUp()
    {
        LaunchpadProject project = BoomBap();
        int toms = LaunchpadExampleBuilder.Toms;
        project.Pads.RemoveAll(pad => pad.Bank == KitBank && pad.Index % 8 == toms && pad.Index / 8 < 5);

        IReadOnlyList<LaunchpadPadAssignment> fills = LaunchpadSuggestions.FillColumn(project, KitBank, toms, RealCatalog.Index);

        Assert.NotEmpty(fills);
        Assert.All(fills, fill => Assert.Equal(toms, fill.PadIndex % 8));
        Assert.Equal(fills.Count, fills.Select(fill => fill.SampleName).Distinct(StringComparer.Ordinal).Count());
        Assert.All(fills, fill => Assert.True(IsA(SampleNamed(fill.SampleName), "tom"), fill.SampleName));
        Assert.Equal(fills.OrderByDescending(fill => fill.PadIndex).Select(fill => fill.PadIndex), fills.Select(fill => fill.PadIndex));
    }

    [Fact]
    public void FillColumn_AFullColumn_HasNothingToFill()
    {
        LaunchpadProject project = BoomBap();
        foreach (int row in Enumerable.Range(0, 8))
        {
            if (!project.Pads.Any(pad => pad.Bank == KitBank && pad.Index == Pad(row, 0)))
            {
                project.Pads.Add(new LaunchpadPad { Bank = KitBank, Index = Pad(row, 0), ClipPath = "/x/Kick Vinyl Thud.wav", Label = "x" });
            }
        }

        Assert.Empty(LaunchpadSuggestions.FillColumn(project, KitBank, 0, RealCatalog.Index));
    }

    [Fact]
    public void SwapKit_ToThe909_SwapsDrumsForTheSameInstrumentsOfThatKit()
    {
        LaunchpadProject project = BoomBap();

        IReadOnlyList<LaunchpadPadAssignment> swaps = LaunchpadSuggestions.SwapKit(project, KitBank, "909", RealCatalog.Index);

        Assert.NotEmpty(swaps);
        Assert.All(swaps, swap =>
        {
            Sample replacement = SampleNamed(swap.SampleName);
            Sample original = LaunchpadSuggestions.SampleOf(project.Pads.First(pad => pad.Bank == KitBank && pad.Index == swap.PadIndex), RealCatalog.Index)!;
            Assert.Equal("909", replacement.Classification.Kit?.Value);
            Assert.Equal(original.Classification.Instrument.Value, replacement.Classification.Instrument.Value);
        });
        Assert.Equal(swaps.Count, swaps.Select(swap => swap.SampleName).Distinct(StringComparer.Ordinal).Count());
    }
    #endregion
}
