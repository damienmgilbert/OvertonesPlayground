namespace OvertonesPlayground.Tests.ViewModels;

public sealed class SampleRowViewModelTests
{
    #region Public methods
    [Fact]
    public void Badges_CoverLengthStereoLoudnessPitchTempoAndContent()
    {
        Sample loop = TestSamples.Make("Groove", "drum-loop", durationSeconds: 5.0, image: StereoImage.Wide, lufs: -9, tempoBpm: 120, content: ContentType.Loop);
        Sample bass = TestSamples.Make("Bass", "bass-sub", durationSeconds: 0.25, fundamentalHz: 55, lufs: -30);

        Assert.Equal(["5.0 s", "Wide", "VeryLoud", "120 BPM", "Loop"], new SampleRowViewModel(loop).Badges);
        Assert.Equal(["250 ms", "Mono", "Quiet", "A1"], new SampleRowViewModel(bass).Badges);
    }

    [Fact]
    public void DualMonoAndOutOfPhase_AreShownInPlainWords()
    {
        Assert.Contains("Mono", new SampleRowViewModel(TestSamples.Make("A", image: StereoImage.DualMono)).Badges);
        Assert.Contains("Out of phase", new SampleRowViewModel(TestSamples.Make("B", image: StereoImage.OutOfPhase)).Badges);
    }

    [Theory]
    [InlineData(0.048, "48 ms")]
    [InlineData(0.9994, "999 ms")]
    [InlineData(1.0, "1.0 s")]
    [InlineData(12.34, "12.3 s")]
    [InlineData(127.4, "2:07")]
    public void FormatDuration_UsesTheShortestReadableUnit(double seconds, string expected) { Assert.Equal(expected, SampleRowViewModel.FormatDuration(seconds)); }
    [Fact]
    public void NeedsReview_MirrorsTheClassification()
    {
        Assert.True(new SampleRowViewModel(TestSamples.Make("A", needsReview: true)).NeedsReview);
        Assert.False(new SampleRowViewModel(TestSamples.Make("B")).NeedsReview);
    }

    [Fact]
    public void SelectionAndAuditionState_RaiseChangeNotifications()
    {
        SampleRowViewModel row = new(TestSamples.Make("A"));
        List<string?> changed = [];
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        row.IsSelected = true;
        row.IsPreviewing = true;

        Assert.Equal([nameof(SampleRowViewModel.IsSelected), nameof(SampleRowViewModel.IsPreviewing)], changed);
    }

    [Fact]
    public void Subtitle_IsTheInstrumentPathAndTheKit()
    {
        SampleRowViewModel row = new(TestSamples.Make("Hihat Closed X", "hihat-closed", kit: "909"));

        Assert.Equal("Percussion > Hi-Hat > Closed Hi-Hat  ·  Roland 909", row.Subtitle);
        Assert.Equal("Hihat Closed X", row.Name);
    }

    [Fact]
    public void Subtitle_UnclassifiedSound_SaysUnclassified() { Assert.Equal("Unclassified", new SampleRowViewModel(TestSamples.Make("Mystery", "unclassified", 0.0)).Subtitle); }
    #endregion
}
