namespace OvertonesPlayground.Tests.Ontology;

public sealed class SampleTests
{
    #region Private methods
    private static RhythmFacet Rhythm(double? detectedBpm, double confidence) => new(detectedBpm, confidence, 2.0, 16, null, null, null, false);

    private static Sample WithRhythm(RhythmFacet? rhythm, double? namedBpm = null) => TestSamples.Make("Loop", tempoBpm: namedBpm) with { Rhythm = rhythm };
    #endregion

    #region Public methods
    [Theory]
    [InlineData(0.30)]
    [InlineData(0.31)]
    [InlineData(1.0)]
    public void EffectiveTempoBpm_NoNameAndAConfidentDetection_UsesTheDetection(double confidence) { Assert.Equal(128.0, WithRhythm(Rhythm(128, confidence)).EffectiveTempoBpm); }
    [Theory]
    [InlineData(0.299)]
    [InlineData(0.1)]
    [InlineData(0.0)]
    public void EffectiveTempoBpm_NoNameAndAWeakDetection_HasNoTempo(double confidence) { Assert.Null(WithRhythm(Rhythm(128, confidence)).EffectiveTempoBpm); }
    [Fact]
    public void EffectiveTempoBpm_NothingDetectedAndNothingNamed_HasNoTempo()
    {
        Assert.Null(WithRhythm(null).EffectiveTempoBpm);
        Assert.Null(WithRhythm(Rhythm(null, 0.0)).EffectiveTempoBpm);
    }

    [Fact]
    public void EffectiveTempoBpm_TheTempoInTheNameWinsOverAConfidentDetection() { Assert.Equal(90.0, WithRhythm(Rhythm(180, 0.9), namedBpm: 90).EffectiveTempoBpm); }
    [Fact]
    public void TrustedBpm_IsNotWrittenToTheCatalog()
    {
        SampleCatalog catalog = SampleCatalog.Create([WithRhythm(Rhythm(128, 0.9))]);

        using MemoryStream stream = new();
        SampleCatalogSerializer.Serialize(catalog, stream);
        string json = System.Text.Encoding.UTF8.GetString(stream.ToArray());

        Assert.Contains("\"detectedBpm\":128", json, StringComparison.Ordinal);
        Assert.DoesNotContain("trustedBpm", json, StringComparison.OrdinalIgnoreCase);
    }
    #endregion
}
