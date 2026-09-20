namespace OvertonesPlayground.Tests.Ontology;

public sealed class CorpusClassifierTests
{
    #region Private methods
    private static Sample Placeholder(Sample sample) => sample with { Classification = SampleClassification.Placeholder(sample.Name.ToLowerInvariant()) };

    private static Sample Unlabelled(string name, double centroidHz, double flatness, double duration, double decayMs, TonalCharacter character = TonalCharacter.Unpitched) => TestSamples.Make(name, "unclassified", 0.0, centroidHz, flatness, duration, decayMs: decayMs, character: character) with
    {
        Classification = SampleClassification.Placeholder(name.ToLowerInvariant()),
    };
    #endregion

    #region Public methods
    [Fact]
    public void Classify_ANameThatContradictsThePlainAudio_IsFlaggedForReview()
    {
        List<Sample> corpus = [.. TestSamples.DrumKit(12).Select(Placeholder)];
        Sample liar = Placeholder(TestSamples.Make("Kick Cyanide", "kick", 0.95, centroidHz: 8100, flatness: 0.63, durationSeconds: 0.12, decayMs: 60, character: TonalCharacter.Unpitched | TonalCharacter.Noisy));
        corpus.Add(liar);

        IReadOnlyList<Sample> classified = new CorpusClassifier().Classify(corpus);

        Sample result = classified.Single(s => s.Name == "Kick Cyanide");
        Assert.Equal("kick", result.Classification.Instrument.Value);
        Assert.True(result.Classification.NeedsReview);
        Assert.Contains(result.Classification.ReviewReasons, reason => reason.Contains("Kick", StringComparison.Ordinal));
    }

    [Fact]
    public void Classify_ANameWithoutInstrumentWordAndAKickLikeSound_IsLabelledByItsNeighbours()
    {
        List<Sample> corpus = [.. TestSamples.DrumKit(12).Select(Placeholder)];
        corpus.Add(Unlabelled("Mystery Boom", 88, 0.05, 0.5, 250, TonalCharacter.Pitched));

        IReadOnlyList<Sample> classified = new CorpusClassifier().Classify(corpus);

        Sample mystery = classified.Single(s => s.Name == "Mystery Boom");
        Assert.Equal("kick", mystery.Classification.Instrument.Value);
        Assert.Contains(mystery.Classification.Instrument.Evidence, e => e.Source == EvidenceSource.Signal);
    }

    [Fact]
    public void Classify_EmbeddedAcidTempo_FillsInWhenTheNameHasNone()
    {
        Sample sample = Placeholder(TestSamples.Make("Groove Thing", "unclassified", 0.0));
        sample = sample with { Technical = sample.Technical! with { EmbeddedTempoBpm = 96.0 } };

        Sample classified = new CorpusClassifier().Classify([sample])[0];

        Assert.Equal(96.0, classified.Classification.Attributes.TempoBpm);
        Assert.Contains(classified.Classification.ContentType.Evidence, e => e.Source == EvidenceSource.Metadata);
    }

    [Fact]
    public void Classify_KeepsFacetsAndIdentityUntouched()
    {
        Sample original = Placeholder(TestSamples.DrumKit(2)[0]);

        Sample classified = new CorpusClassifier().Classify([original])[0];

        Assert.Equal(original.Asset, classified.Asset);
        Assert.Equal(original.Spectral, classified.Spectral);
        Assert.Equal(original.Dynamics, classified.Dynamics);
    }

    [Fact]
    public void Classify_ManualOverrides_AreAppliedLast()
    {
        List<Sample> corpus = [.. TestSamples.DrumKit(6).Select(Placeholder)];
        ClassificationOverrides overrides = new([new OverrideEntry { File = "Kick Test 1.wav", Instrument = "tom", Note = "test" }]);

        IReadOnlyList<Sample> classified = new CorpusClassifier(overrides).Classify(corpus);

        Assert.Equal("tom", classified.Single(s => s.Id == "Kick Test 1.wav").Classification.Instrument.Value);
        Assert.Equal("kick", classified.Single(s => s.Id == "Kick Test 2.wav").Classification.Instrument.Value);
    }

    [Fact]
    public void Classify_TempoAndKeyFromTheName_LandInTheAttributes()
    {
        Sample loop = Placeholder(TestSamples.Make("Banjo Arpeggio E Minor 100 bpm", "unclassified", 0.0, durationSeconds: 4));

        Sample classified = new CorpusClassifier().Classify([loop])[0];

        Assert.Equal(100.0, classified.Classification.Attributes.TempoBpm);
        Assert.Equal("E min", classified.Classification.Attributes.KeyName());
        Assert.Equal("banjo", classified.Classification.Instrument.Value);
        Assert.Equal(ContentType.Loop, classified.Classification.ContentType.Value);
    }

    [Fact]
    public void Classify_UnsupportedFileWithNoFacets_IsStillLabelledFromItsName()
    {
        Sample unsupported = new(new SampleAsset("Atmos Wavetable Like.aif", 100, "h"), AnalysisStatus.UnsupportedFormat, null, null, null, null, null, null, SampleClassification.Placeholder("atmos wavetable like"));

        Sample classified = new CorpusClassifier().Classify([unsupported])[0];

        Assert.Equal(AnalysisStatus.UnsupportedFormat, classified.Status);
        Assert.Equal("atmosphere", classified.Classification.Instrument.Value);
    }

    [Fact]
    public void Classify_UsesTheNamesToLabelTheCorpus()
    {
        List<Sample> corpus = [.. TestSamples.DrumKit(10).Select(Placeholder)];

        IReadOnlyList<Sample> classified = new CorpusClassifier().Classify(corpus);

        Assert.Equal(corpus.Count, classified.Count);
        Assert.All(classified.Where(s => s.Name.StartsWith("Kick", StringComparison.Ordinal)), s => Assert.Equal("kick", s.Classification.Instrument.Value));
        Assert.All(classified.Where(s => s.Name.StartsWith("Hihat Closed", StringComparison.Ordinal)), s => Assert.Equal("hihat-closed", s.Classification.Instrument.Value));
        Assert.All(classified, s => Assert.False(s.Classification.NeedsReview, $"{s.Name}: {string.Join("; ", s.Classification.ReviewReasons)}"));
    }

    [Fact]
    public void EvaluateSignalAgreement_OnATightlyClusteredKit_AgreesAlmostAlways()
    {
        IReadOnlyList<Sample> classified = new CorpusClassifier().Classify([.. TestSamples.DrumKit(12).Select(Placeholder)]);

        SignalEvaluation evaluation = CorpusClassifier.EvaluateSignalAgreement(classified);

        Assert.Equal(36, evaluation.Evaluated);
        Assert.True(evaluation.Accuracy > 0.95, $"accuracy {evaluation.Accuracy:P0}");
        Assert.True(evaluation.CoreAccuracy > 0.95);
        Assert.Equal(evaluation.Evaluated - evaluation.Agreed, evaluation.Disagreements.Count);
    }

    [Fact]
    public void SignalClassifier_NeverLetsASampleVoteForItself()
    {
        FingerprintSpace space = new(TestSamples.DrumKit(4));
        Sample only = TestSamples.Make("Solo", "kick");
        FingerprintSpace singleSpace = new([only]);
        SignalHeuristicClassifier classifier = new(singleSpace, [new TrainingExample(only.Id, singleSpace.Vector(only)!, "kick")]);

        Assert.Empty(classifier.Predict(only));
        Assert.NotNull(space.Vector(TestSamples.DrumKit(1)[0]));
    }
    #endregion
}
