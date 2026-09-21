using OvertonesPlayground.Ontology.Theory;

namespace OvertonesPlayground.Tests.Ontology;

///<summary>
///The sample recommender against the real bank: it keeps to the slot's instrument, the key and the tempo, prefers the project's
///kits, says why, and does not offer near copies.
///</summary>
public sealed class SampleRecommenderTests
{
    #region Private methods
    private static Sample Named(string name) => RealCatalog.Index.Find(name + ".wav") ?? throw new InvalidOperationException($"No sample '{name}'.");

    private static SampleRecommender Recommender() => new(RealCatalog.Index);

    private static bool IsA(Sample sample, string key) => Taxonomies.Instruments.Get(sample.Classification.Instrument.Value).IsA(key);

    private static List<Sample> VinylKit() =>
        [.. RealCatalog.Index.All.Where(s => s.Classification.Kit?.Value == "vinyl" && IsA(s, "kick")).Take(3), .. RealCatalog.Index.All.Where(s => s.Classification.Kit?.Value == "vinyl" && IsA(s, "snare")).Take(3)];
    #endregion

    #region Public methods
    [Fact]
    public void Recommend_ForAHatSlot_OffersOnlyHatsAndPrefersTheProjectsKit()
    {
        RecommendationContext context = new() { InUse = VinylKit(), SlotInstruments = ["hihat"], TempoBpm = 90 };

        IReadOnlyList<Recommendation> recommendations = Recommender().Recommend(context, 8);

        Assert.Equal(8, recommendations.Count);
        Assert.All(recommendations, r => Assert.True(IsA(r.Sample, "hihat"), r.Sample.Name));
        Assert.Equal("vinyl", recommendations[0].Sample.Classification.Kit?.Value);
        Assert.Contains("same kit (vinyl)", recommendations[0].Reasons);
    }

    [Fact]
    public void Recommend_WithAKey_LeavesOutWhatClashesAndSaysWhatFits()
    {
        Key key = Key.Parse("E minor");
        RecommendationContext context = new() { SlotInstruments = ["keys", "synth", "stab"], Key = key, WantsLoop = false };

        IReadOnlyList<Recommendation> recommendations = Recommender().Recommend(context, 12);

        Assert.NotEmpty(recommendations);
        Assert.All(recommendations, r => Assert.NotEqual(KeyFit.Clashes, KeyCompatibility.Judge(r.Sample, key)));
        Assert.Contains(recommendations, r => r.Reasons.Contains("in E minor"));
    }

    [Fact]
    public void Recommend_LoopsAtAnotherTempo_AreLeftOut()
    {
        RecommendationContext context = new() { SlotInstruments = ["drum-loop", "mixed-loop"], TempoBpm = 90, WantsLoop = true };

        IReadOnlyList<Recommendation> recommendations = Recommender().Recommend(context, 10);

        Assert.NotEmpty(recommendations);
        Assert.All(recommendations, r => Assert.Equal(90, r.Sample.Classification.Attributes.TempoBpm!.Value, 0.5));
    }

    [Fact]
    public void Recommend_NeverOffersASampleAlreadyInUse()
    {
        List<Sample> inUse = [.. RealCatalog.Index.All.Where(s => IsA(s, "kick")).Take(40)];
        RecommendationContext context = new() { InUse = inUse, SlotInstruments = ["kick"] };

        IReadOnlyList<Recommendation> recommendations = Recommender().Recommend(context, 8);

        Assert.DoesNotContain(recommendations, r => inUse.Any(s => s.Id == r.Sample.Id));
    }

    [Fact]
    public void Recommend_ReplacingASound_PutsSimilarSoundsFirst()
    {
        Sample snare = VinylKit().First(s => IsA(s, "snare"));
        RecommendationContext context = new() { InUse = [snare], SlotInstruments = ["snare"], Replacing = snare };

        IReadOnlyList<Recommendation> recommendations = Recommender().Recommend(context, 5);
        IReadOnlyList<(Sample Sample, double Distance)> nearest = RealCatalog.Index.FindSimilar(snare, 60);

        Assert.All(recommendations, r => Assert.True(IsA(r.Sample, "snare")));
        Assert.Contains(recommendations, r => nearest.Take(30).Any(n => n.Sample.Id == r.Sample.Id));
    }

    [Fact]
    public void Recommend_WithAKit_OffersOnlyThatKit()
    {
        RecommendationContext context = new() { SlotInstruments = ["snare"], Kit = "909" };

        IReadOnlyList<Recommendation> recommendations = Recommender().Recommend(context, 6);

        Assert.NotEmpty(recommendations);
        Assert.All(recommendations, r => Assert.Equal("909", r.Sample.Classification.Kit?.Value));
    }

    [Fact]
    public void Recommend_WithAnEightOhEightBass_MarksBoomyKicks()
    {
        List<Sample> bass = [.. RealCatalog.Index.All.Where(s => IsA(s, "bass-808")).Take(3)];
        RecommendationContext context = new() { InUse = bass, SlotInstruments = ["kick"] };

        IReadOnlyList<Recommendation> recommendations = Recommender().Recommend(context, 20);

        Assert.All(recommendations.Where(r => r.Sample.Spectral!.Bands.Sub > SampleRecommender.MaxSubUnderBass), r => Assert.Contains(r.Reasons, reason => reason.Contains("crowd the bass", StringComparison.Ordinal)));
        Assert.True(recommendations[0].Sample.Spectral!.Bands.Sub <= SampleRecommender.MaxSubUnderBass);
    }

    [Fact]
    public void InferKey_FromChordsAndNotes_FindsTheirKey()
    {
        List<Sample> samples = [Named("Organ Pad LDre Amin7"), Named("Organ Pad LDre Dmin7"), Named("Organ Pad LDre Emin7"), Named("Organ Pad LDre FMaj7")];

        Key? key = SampleRecommender.InferKey(samples);

        Assert.NotNull(key);
        Assert.Equal("A minor", key.Name);
    }

    [Fact]
    public void InferKey_WithNothingPitched_IsNull() =>
        Assert.Null(SampleRecommender.InferKey(VinylKit()));

    [Fact]
    public void InferTempo_IsTheLoopsTempo() =>
        Assert.Equal(90, SampleRecommender.InferTempo([Named("Break Ghosts 90 bpm"), Named("80s Beat 90 bpm"), .. VinylKit()]));
    #endregion
}
