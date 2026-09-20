namespace OvertonesPlayground.Tests.Ontology;

public sealed class EvidenceFusionTests
{
    #region Fields
    private readonly EvidenceFusion _fusion = new();
    #endregion

    #region Private methods
    private SampleClassification Fuse(string name, IReadOnlyList<ClassificationProposal> votes, OverrideEntry? manual = null)
    {
        Sample sample = TestSamples.Make(name);
        return _fusion.Fuse(new ClassificationContext(FilenameParser.Parse(name), sample), votes, manual);
    }

    private static ClassificationProposal Vote(ClassificationAxis axis, string value, double confidence, EvidenceSource source = EvidenceSource.Filename) => new(axis, value, confidence, new Evidence(source, $"{source} says {value}"));
    #endregion

    #region Public methods
    [Fact]
    public void Fuse_AudioOnlyGuessBelowThreshold_IsKeptAsAlternateNotAsTheLabel()
    {
        SampleClassification classification = Fuse("Vinyl Dirt", [Vote(ClassificationAxis.Instrument, "hihat", 0.22, EvidenceSource.Signal)]);

        Assert.Equal("unclassified", classification.Instrument.Value);
        Assert.Equal(0.0, classification.Instrument.Confidence);
        Assert.Contains(classification.AlternateInstruments, label => label.Value == "hihat");
        Assert.True(classification.NeedsReview);
        Assert.Contains(classification.ReviewReasons, reason => reason.Contains("only suggests Hi-Hat"));
    }

    [Fact]
    public void Fuse_AudioSupportsTheFamilyOfADeeperNameLabel()
    {
        SampleClassification classification = Fuse("Hihat Closed X", [Vote(ClassificationAxis.Instrument, "hihat-closed", 0.9), Vote(ClassificationAxis.Instrument, "hihat", 0.8, EvidenceSource.Signal)]);

        Assert.Equal("hihat-closed", classification.Instrument.Value);
        Assert.True(classification.Instrument.Confidence > 0.9);
        Assert.False(classification.NeedsReview);
    }

    [Fact]
    public void Fuse_ConfidentAudioOnlyGuess_BecomesTheLabelAtReducedConfidence()
    {
        SampleClassification classification = Fuse("Mystery", [Vote(ClassificationAxis.Instrument, "snare", 0.85, EvidenceSource.Signal)]);

        Assert.Equal("snare", classification.Instrument.Value);
        Assert.True(classification.Instrument.Confidence < 0.85);
        Assert.Equal(EvidenceSource.Signal, classification.Instrument.Evidence[0].Source);
    }

    [Fact]
    public void Fuse_ConfidentNameContradictedByConfidentAudio_IsFlaggedForReview()
    {
        SampleClassification classification = Fuse("Kick X", [Vote(ClassificationAxis.Instrument, "kick", 0.95), Vote(ClassificationAxis.Instrument, "hihat", 0.8, EvidenceSource.Signal)]);

        Assert.Equal("kick", classification.Instrument.Value);
        Assert.True(classification.NeedsReview);
        Assert.Contains(classification.ReviewReasons, reason => reason.Contains("Kick") && reason.Contains("Hi-Hat"));
    }

    [Fact]
    public void Fuse_ContentTypeAndOrigin_TakeTheStrongestCombinedVote()
    {
        SampleClassification classification = Fuse("Break X 90 bpm", [Vote(ClassificationAxis.ContentType, nameof(ContentType.Loop), 0.65), Vote(ClassificationAxis.ContentType, nameof(ContentType.Break), 0.85), Vote(ClassificationAxis.Origin, nameof(SoundOrigin.Sampled), 0.6),]);

        Assert.Equal(ContentType.Break, classification.ContentType.Value);
        Assert.Equal(SoundOrigin.Sampled, classification.Origin.Value);
    }

    [Fact]
    public void Fuse_GenericFamiliesAreNotFlaggedBecauseTheirAudioCouldBeAnything()
    {
        SampleClassification classification = Fuse("FX Brute Hit", [Vote(ClassificationAxis.Instrument, "sfx", 0.95), Vote(ClassificationAxis.Instrument, "snare", 0.8, EvidenceSource.Signal)]);

        Assert.Equal("sfx", classification.Instrument.Value);
        Assert.False(classification.NeedsReview);
    }

    [Fact]
    public void Fuse_GenericLoopWithAConfidentDrumLoopAudioVote_IsRefinedToDrumLoop()
    {
        SampleClassification classification = Fuse("Dark Garage 130 bpm", [Vote(ClassificationAxis.Instrument, "mixed-loop", 0.55), Vote(ClassificationAxis.Instrument, "drum-loop", 0.7, EvidenceSource.Signal)]);

        Assert.Equal("drum-loop", classification.Instrument.Value);
    }

    [Fact]
    public void Fuse_GenericLoopWithNoDrumEvidence_StaysMixedLoop()
    {
        SampleClassification classification = Fuse("Dark Garage 130 bpm", [Vote(ClassificationAxis.Instrument, "mixed-loop", 0.55), Vote(ClassificationAxis.Instrument, "drum-loop", 0.3, EvidenceSource.Signal)]);

        Assert.Equal("mixed-loop", classification.Instrument.Value);
    }

    [Fact]
    public void Fuse_InstrumentFromAnOverride_StillGetsThatInstrumentsDefaultContentType()
    {
        OverrideEntry manual = new() { File = "Rise.wav", Instrument = "riser" };

        SampleClassification classification = Fuse("Rise", [], manual);

        Assert.Equal(ContentType.Transition, classification.ContentType.Value);
        Assert.Equal(EvidenceSource.Manual, classification.ContentType.Evidence[0].Source);
    }

    [Fact]
    public void Fuse_KitAndStyle_AreOptional()
    {
        SampleClassification classification = Fuse("Kick 909", [Vote(ClassificationAxis.Kit, "909", 0.9), Vote(ClassificationAxis.Style, "house", 0.75)]);

        Assert.Equal("909", classification.Kit?.Value);
        Assert.Equal("house", classification.Style?.Value);
    }

    [Fact]
    public void Fuse_ManualOverride_WinsWithFullConfidenceAndClearsReview()
    {
        OverrideEntry manual = new() { File = "Kick X.wav", Instrument = "tom", Note = "maintainer decision" };

        SampleClassification classification = Fuse("Kick X", [Vote(ClassificationAxis.Instrument, "kick", 0.95), Vote(ClassificationAxis.Instrument, "hihat", 0.8, EvidenceSource.Signal)], manual);

        Assert.Equal("tom", classification.Instrument.Value);
        Assert.Equal(1.0, classification.Instrument.Confidence);
        Assert.Equal(EvidenceSource.Manual, classification.Instrument.Evidence[0].Source);
        Assert.Equal("maintainer decision", classification.Instrument.Evidence[0].Detail);
        Assert.False(classification.NeedsReview);
        Assert.Empty(classification.AlternateInstruments);
    }

    [Fact]
    public void Fuse_ManualOverrideOfOneAxis_LeavesTheOthersAlone()
    {
        OverrideEntry manual = new() { File = "Kick X.wav", Style = "jazz", ContentType = "Loop", Origin = "Acoustic", Kit = "808" };

        SampleClassification classification = Fuse("Kick X", [Vote(ClassificationAxis.Instrument, "kick", 0.95)], manual);

        Assert.Equal("kick", classification.Instrument.Value);
        Assert.Equal("jazz", classification.Style?.Value);
        Assert.Equal(ContentType.Loop, classification.ContentType.Value);
        Assert.Equal(SoundOrigin.Acoustic, classification.Origin.Value);
        Assert.Equal("808", classification.Kit?.Value);
    }

    [Fact]
    public void Fuse_NameAndAudioAgree_RaisesConfidenceAboveEitherAlone()
    {
        SampleClassification byName = Fuse("Kick X", [Vote(ClassificationAxis.Instrument, "kick", 0.8)]);
        SampleClassification both = Fuse("Kick X", [Vote(ClassificationAxis.Instrument, "kick", 0.8), Vote(ClassificationAxis.Instrument, "kick", 0.8, EvidenceSource.Signal)]);

        Assert.True(both.Instrument.Confidence > byName.Instrument.Confidence);
        Assert.Equal(2, both.Instrument.Evidence.Count);
        Assert.False(both.NeedsReview);
    }

    [Fact]
    public void Fuse_NoEvidenceAtAll_IsUnclassifiedAndFlagged()
    {
        SampleClassification classification = Fuse("Zzz", []);

        Assert.Equal("unclassified", classification.Instrument.Value);
        Assert.True(classification.NeedsReview);
        Assert.Equal(ContentType.Unknown, classification.ContentType.Value);
        Assert.Equal(SoundOrigin.Unknown, classification.Origin.Value);
        Assert.Null(classification.Kit);
        Assert.Null(classification.Style);
    }

    [Fact]
    public void Fuse_SecondInstrumentWordInTheName_BecomesAnAlternate()
    {
        SampleClassification classification = Fuse("Snare Hat Combo", [Vote(ClassificationAxis.Instrument, "snare", 0.95), Vote(ClassificationAxis.Instrument, "hihat", 0.5)]);

        Assert.Equal("snare", classification.Instrument.Value);
        Assert.Single(classification.AlternateInstruments);
        Assert.Equal("hihat", classification.AlternateInstruments[0].Value);
    }

    [Fact]
    public void Fuse_SeveralVotesFromOneSource_CountOnce()
    {
        SampleClassification classification = Fuse("Kick X", [Vote(ClassificationAxis.Instrument, "kick", 0.8), Vote(ClassificationAxis.Instrument, "kick", 0.7)]);

        Assert.Equal(0.8, classification.Instrument.Confidence, 0.001);
    }

    [Fact]
    public void Overrides_FromJson_AreFoundCaseInsensitivelyByFileName()
    {
        ClassificationOverrides overrides = ClassificationOverrides.FromJson(
                                            """
            // comments are allowed
            { "overrides": [ { "file": "Vinyl Dirt 1.wav", "instrument": "noise", "note": "why" } ] }
            """);

        Assert.Equal(1, overrides.Count);
        Assert.Equal("noise", overrides.Find("vinyl dirt 1.WAV")?.Instrument);
        Assert.Null(overrides.Find("Other.wav"));
        Assert.Equal(0, ClassificationOverrides.Empty.Count);
    }

    [Fact]
    public void Overrides_MissingFile_LoadsAsEmpty() { Assert.Equal(0, ClassificationOverrides.Load(Path.Combine(Path.GetTempPath(), $"no-such-overrides-{Guid.NewGuid():N}.json")).Count); }
    #endregion
}
