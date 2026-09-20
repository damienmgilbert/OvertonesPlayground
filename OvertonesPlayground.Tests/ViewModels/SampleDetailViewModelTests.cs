namespace OvertonesPlayground.Tests.ViewModels;

public sealed class SampleDetailViewModelTests
{
    #region Fields
    private static readonly SampleIndex _index = new(TestSamples.Library());
    #endregion

    #region Private methods
    private static SampleDetailViewModel Detail(string id) => new(_index.Find(id)!, _index);

    private static string Fact(SampleDetailViewModel detail, string label) => detail.Facts.Single(f => f.Label == label).Value;
    #endregion

    #region Public methods
    [Fact]
    public void Bands_AreSevenBarsInFrequencyOrderThatSumToOne()
    {
        SampleDetailViewModel detail = Detail("Kick Test 1.wav");

        Assert.Equal(7, detail.Bands.Count);
        Assert.StartsWith("Sub", detail.Bands[0].Band, StringComparison.Ordinal);
        Assert.StartsWith("Air", detail.Bands[6].Band, StringComparison.Ordinal);
        Assert.Equal(1.0, detail.Bands.Sum(b => b.Fraction), 0.001);
        Assert.Equal("100 %", detail.Bands.Single(b => b.Fraction > 0.5).Percent);
    }

    [Fact]
    public void Evidence_ListsWhyTheInstrumentWasChosen() { Assert.Equal(["Filename: test"], Detail("Kick 909 DMX 1.wav").Evidence); }
    [Fact]
    public void Facts_ClippingAndDcOffset_AreCalledOut()
    {
        Sample clipped = TestSamples.Make("Hot");
        clipped = clipped with { Dynamics = clipped.Dynamics! with { ClippedSamples = 1200, DcOffset = 0.05 } };

        SampleDetailViewModel detail = new(clipped, new SampleIndex([clipped]));

        Assert.Contains("1,200 clipped samples", Fact(detail, "Watch out"), StringComparison.Ordinal);
        Assert.Contains("DC offset 5", Fact(detail, "Watch out"), StringComparison.Ordinal);
    }

    [Fact]
    public void Facts_DescribeTheClassificationAndEveryAcousticCategory()
    {
        SampleDetailViewModel detail = Detail("808 Oracle 1.wav");

        Assert.Equal("808 Oracle 1", detail.Title);
        Assert.Equal("Bass > 808 Bass  (95 % sure)", Fact(detail, "Instrument"));
        Assert.Equal("Drum Machine > Roland 808", Fact(detail, "Kit"));
        Assert.Contains("One Shot", Fact(detail, "Type"), StringComparison.Ordinal);
        Assert.Contains("24-bit 44.1 kHz mono", Fact(detail, "File"), StringComparison.Ordinal);
        Assert.Contains("2.5 s", Fact(detail, "Length"), StringComparison.Ordinal);
        Assert.Contains("-9 LUFS", Fact(detail, "Loudness"), StringComparison.Ordinal);
        Assert.Contains("Impulsive", Fact(detail, "Envelope"), StringComparison.Ordinal);
        Assert.Contains("centroid 45 Hz", Fact(detail, "Brightness"), StringComparison.Ordinal);
        Assert.Contains("E1", Fact(detail, "Pitch"), StringComparison.Ordinal);
        Assert.Contains("41.2 Hz", Fact(detail, "Pitch"), StringComparison.Ordinal);
        Assert.Contains("Sub", Fact(detail, "Character"), StringComparison.Ordinal);
        Assert.Equal("Mono", Fact(detail, "Stereo"));
        Assert.Equal("key E", Fact(detail, "Tempo / key"));
    }

    [Fact]
    public void Facts_OmitKitStyleAndTempoWhenThereAreNone()
    {
        SampleDetailViewModel detail = Detail("Vinyl Dirt 1.wav");

        Assert.DoesNotContain(detail.Facts, f => f.Label == "Kit");
        Assert.DoesNotContain(detail.Facts, f => f.Label == "Style");
        Assert.DoesNotContain(detail.Facts, f => f.Label == "Tempo / key");
    }

    [Fact]
    public void Facts_TempoFromTheName_IsLabelledAsSuch()
    {
        SampleDetailViewModel detail = Detail("Break Ghosts 90 bpm.wav");

        Assert.Equal("90 BPM (from the name)", Fact(detail, "Tempo / key"));
        Assert.Contains("Wide", Fact(detail, "Stereo"), StringComparison.Ordinal);
        Assert.Equal("no stable pitch", Fact(detail, "Pitch"));
    }

    [Fact]
    public void Facts_TypeOmitsAnOriginNobodyDetermined_ButShowsOneThatIs()
    {
        Assert.Equal("Texture", Fact(Detail("Vinyl Dirt 1.wav"), "Type"));
        Assert.Equal("Break", Fact(Detail("Break Ghosts 90 bpm.wav"), "Type"));
        Assert.Equal("One Shot  ·  Drum Machine", Fact(Detail("Kick 909 DMX 1.wav"), "Type"));
    }

    [Fact]
    public void Facts_UnclassifiedSound_SaysSo()
    {
        Sample unknown = TestSamples.Make("Mystery", "unclassified", 0.0);

        SampleDetailViewModel detail = new(unknown, new SampleIndex([unknown]));

        Assert.Equal("Unclassified", Fact(detail, "Instrument"));
    }

    [Fact]
    public void Related_GroupsVariationsKitMatesSimilarSoundsAndTempoMatches()
    {
        SampleDetailViewModel kick = Detail("Kick 909 DMX 1.wav");
        SampleDetailViewModel loop = Detail("Break Ghosts 90 bpm.wav");

        string[] kickGroups = [.. kick.Related.Select(g => g.Title)];
        Assert.Contains("Variations", kickGroups);
        Assert.Contains("Goes with (same kit)", kickGroups);
        Assert.Contains("Sounds like this", kickGroups);
        Assert.Equal("Kick 909 DMX 2", kick.Related.Single(g => g.Title == "Variations").Items.Single().Name);
        Assert.Equal("Snare 909 DMX 1", kick.Related.Single(g => g.Title == "Goes with (same kit)").Items.Single().Name);
        Assert.Contains(loop.Related, g => g.Title == "Same tempo" && g.Items.Any(i => i.Name == "Groove B 180 bpm"));
        Assert.All(kick.Related.SelectMany(g => g.Items), item => Assert.NotEqual("Kick 909 DMX 1", item.Name));
    }

    [Fact]
    public void ReviewNotes_AreShownOnlyForFlaggedSounds()
    {
        Sample flagged = TestSamples.Make("Odd One", needsReview: true);
        SampleDetailViewModel review = new(flagged, new SampleIndex([flagged]));

        Assert.True(review.NeedsReview);
        Assert.Equal(["test review"], review.ReviewNotes);
        Assert.False(Detail("Kick 909 DMX 1.wav").NeedsReview);
    }
    #endregion
}
