namespace OvertonesPlayground.Tests.Ontology;

public sealed class SampleIndexTests
{
    #region Private methods
    private static SampleIndex Index() => new(TestSamples.Library());
    #endregion

    #region Public methods
    [Fact]
    public void All_IsOrderedByNameIgnoringCase()
    {
        IReadOnlyList<Sample> all = Index().All;

        Assert.Equal([.. all.Select(s => s.Name).Order(StringComparer.OrdinalIgnoreCase)], all.Select(s => s.Name));
    }

    [Fact]
    public void Any_MatchesEverything_AndNeedsReviewFindsFlaggedSamples()
    {
        SampleIndex index = new([TestSamples.Make("A", needsReview: true), TestSamples.Make("B")]);

        Assert.Equal(2, index.Query(new SampleQuery { Filter = SampleSpecs.Any() }).Count);
        Assert.Equal(["A"], index.Query(new SampleQuery { Filter = SampleSpecs.NeedsReview() }).Select(s => s.Name));
    }

    [Fact]
    public void CountBy_CountsAnyFacetValue()
    {
        IReadOnlyDictionary<StereoImage, int> counts = Index().CountBy(s => s.Stereo?.Image);

        Assert.Equal(1, counts[StereoImage.Wide]);
        Assert.Equal(1, counts[StereoImage.DualMono]);
        Assert.Equal(Index().Count - 2, counts[StereoImage.Mono]);
    }

    [Fact]
    public void CountByInstrument_CountsASampleTowardItsConceptAndEveryAncestor()
    {
        IReadOnlyDictionary<string, int> counts = Index().CountByInstrument();

        Assert.Equal(6, counts["hihat-closed"]);
        Assert.Equal(6, counts["hihat"]);
        Assert.Equal(8, counts["kick"]);
        Assert.Equal(counts["kick"] + counts["snare"] + counts["hihat"], counts["percussion"]);
        Assert.Equal(1, counts["bass-808"]);
        Assert.Equal(counts["percussion"] + 1 + 2 + 1, counts["instrument"]);
        Assert.Equal(25, counts["instrument"]);
    }

    [Fact]
    public void FacetSpecifications_FilterOnEachCategory()
    {
        SampleIndex index = Index();

        Assert.Equal(["Break Ghosts 90 bpm"], index.Query(new SampleQuery { Filter = SampleSpecs.StereoIs(StereoImage.Wide) }).Select(s => s.Name));
        Assert.Equal(["Vinyl Dirt 1"], index.Query(new SampleQuery { Filter = SampleSpecs.StereoIs(StereoImage.DualMono) }).Select(s => s.Name));
        Assert.Equal(["Vinyl Dirt 1"], index.Query(new SampleQuery { Filter = SampleSpecs.ContentTypeIs(ContentType.Texture) }).Select(s => s.Name));
        Assert.Equal(["808 Oracle 1"], index.Query(new SampleQuery { Filter = SampleSpecs.HasCharacter(TonalCharacter.Sub) }).Select(s => s.Name));
        Assert.Equal(["808 Oracle 1"], index.Query(new SampleQuery { Filter = SampleSpecs.Loudness(LoudnessClass.VeryLoud) & SampleSpecs.LufsBetween(-10, 0) }).Select(s => s.Name));
        Assert.Equal(["Break Ghosts 90 bpm", "Vinyl Dirt 1"], index.Query(new SampleQuery { Filter = SampleSpecs.Loudness(LoudnessClass.Quiet) & SampleSpecs.Length(LengthClass.Long) }).Select(s => s.Name));
        Assert.Equal(["Vinyl Dirt 1"], index.Query(new SampleQuery { Filter = SampleSpecs.CentroidBetween(5000, 6000) }).Select(s => s.Name));
        Assert.Equal(["Groove B 180 bpm"], index.Query(new SampleQuery { Filter = SampleSpecs.StyleIs("hip-hop") }).Select(s => s.Name));
        Assert.Equal(3, index.Query(new SampleQuery { Filter = SampleSpecs.KitIs("909") }).Count);
        Assert.Equal(4, index.Query(new SampleQuery { Filter = SampleSpecs.KitIs("drum-machine") }).Count);
        Assert.Equal(2, index.Query(new SampleQuery { Filter = SampleSpecs.OriginIs(SoundOrigin.DrumMachine) }).Count);
        Assert.Equal(["808 Oracle 1"], index.Query(new SampleQuery { Filter = SampleSpecs.PitchClassIs(4) }).Select(s => s.Name));
    }

    [Fact]
    public void Find_LooksUpBySampleId()
    {
        SampleIndex index = Index();

        Assert.Equal("Kick 909 DMX 1", index.Find("Kick 909 DMX 1.wav")?.Name);
        Assert.Null(index.Find("nope.wav"));
    }

    [Fact]
    public void FindSimilar_ForASampleWithoutFacets_IsEmpty()
    {
        Sample bare = new(new SampleAsset("x.aif", 1, "h"), AnalysisStatus.UnsupportedFormat, null, null, null, null, null, null, SampleClassification.Placeholder("x"));
        SampleIndex index = new([bare, TestSamples.Make("K")]);

        Assert.Empty(index.FindSimilar(bare, 3));
    }

    [Fact]
    public void FindSimilar_ReturnsNearestFirstWithDistances()
    {
        SampleIndex index = Index();

        IReadOnlyList<(Sample Sample, double Distance)> neighbours = index.FindSimilar(index.Find("Hihat Closed Test 1.wav")!, 4);

        Assert.Equal(4, neighbours.Count);
        Assert.True(neighbours[0].Distance <= neighbours[1].Distance);
        Assert.All(neighbours, n => Assert.StartsWith("Hihat", n.Sample.Name, StringComparison.Ordinal));
    }

    [Fact]
    public void GetKeyGroup_CollectsSamplesThatShareAPitchClass()
    {
        KeyGroup group = Index().GetKeyGroup(4);

        Assert.Equal(["808 Oracle 1"], group.Members.Select(m => m.Name));
        Assert.Equal("E", group.Name);
        Assert.Equal("Key", group.Kind);
    }

    [Fact]
    public void GetKits_ListsKitsLargestFirst_AndGetKitMembersIncludesDescendants()
    {
        SampleIndex index = Index();

        IReadOnlyList<Kit> kits = index.GetKits();

        Assert.Equal("Roland 909", kits[0].Name);
        Assert.Equal(3, kits[0].Members.Count);
        Assert.Equal("Kit", kits[0].Kind);
        Assert.Equal(4, index.GetKitMembers("drum-machine").Count);
    }

    [Fact]
    public void GetRelationships_LinksVariationsKitMatesTempoKeyAndNeighbours()
    {
        SampleIndex index = Index();
        Sample kick = index.Find("Kick 909 DMX 1.wav")!;

        IReadOnlyList<SampleRelationship> relations = index.GetRelationships(kick);

        Assert.Contains(relations, r => r.Type == SampleRelationType.VariationOf && r.Target.Name == "Kick 909 DMX 2");
        Assert.Contains(relations, r => r.Type == SampleRelationType.Complements && r.Target.Name == "Snare 909 DMX 1");
        Assert.Contains(relations, r => r.Type == SampleRelationType.SimilarTo);
        Assert.DoesNotContain(relations, r => r.Target.Id == kick.Id);
        Assert.All(relations, r => Assert.InRange(r.Strength, 0.0, 1.0));

        IReadOnlyList<SampleRelationship> loopRelations = index.GetRelationships(index.Find("Break Ghosts 90 bpm.wav")!);
        Assert.Contains(loopRelations, r => r.Type == SampleRelationType.TempoCompatible && r.Target.Name == "Groove B 180 bpm");
    }

    [Fact]
    public void GetTempoGroup_FoldsHalfAndDoubleTime()
    {
        SampleIndex index = Index();

        TempoGroup group = index.GetTempoGroup(90);

        Assert.Equal(["Break Ghosts 90 bpm", "Groove B 180 bpm"], group.Members.Select(m => m.Name));
        Assert.Equal("90 BPM", group.Name);
        Assert.Equal("Tempo", group.Kind);
    }

    [Fact]
    public void GetVariations_GroupsNumberedVariationsOfOneSound()
    {
        SampleIndex index = Index();

        VariationSet set = index.GetVariations(index.Find("Kick 909 DMX 1.wav")!);

        Assert.Equal("Variations", set.Kind);
        Assert.Equal(["Kick 909 DMX 1", "Kick 909 DMX 2"], set.Members.Select(m => m.Name));
    }

    [Fact]
    public void InstrumentIs_MatchesTheConceptAndItsDescendants()
    {
        SampleIndex index = Index();

        Assert.Equal(6, index.Query(new SampleQuery { Filter = SampleSpecs.InstrumentIs("hihat") }).Count);
        Assert.Equal(6, index.Query(new SampleQuery { Filter = SampleSpecs.InstrumentIs("hihat-closed") }).Count);
        Assert.Empty(index.Query(new SampleQuery { Filter = SampleSpecs.InstrumentIs("hihat-open") }));
        Assert.True(index.Query(new SampleQuery { Filter = SampleSpecs.InstrumentIs("percussion") }).Count >= 20);
    }

    [Fact]
    public void Limit_CapsTheResults() { Assert.Equal(5, Index().Query(new SampleQuery { Limit = 5 }).Count); }
    [Fact]
    public void SimilarTo_OrdersByAcousticDistanceAndExcludesTheSampleItself()
    {
        SampleIndex index = Index();
        Sample kick = index.Find("Kick Test 1.wav")!;

        IReadOnlyList<Sample> similar = index.Query(new SampleQuery { SimilarTo = kick, Limit = 5 });

        Assert.DoesNotContain(similar, s => s.Id == kick.Id);
        Assert.Equal(5, similar.Count);
        Assert.All(similar.Take(4), s => Assert.StartsWith("Kick", s.Name, StringComparison.Ordinal));
    }

    [Fact]
    public void Sort_OrdersByTheRequestedFacetAndDirection()
    {
        SampleIndex index = Index();

        Assert.Equal("808 Oracle 1", index.Query(new SampleQuery { Sort = SampleSortKey.Name }).First().Name);
        Assert.Equal("Hihat Closed Test 6", index.Query(new SampleQuery { Sort = SampleSortKey.Brightness, Descending = true }).First().Name);
        Assert.Equal("808 Oracle 1", index.Query(new SampleQuery { Sort = SampleSortKey.Loudness, Descending = true }).First().Name);
        Assert.Equal("Break Ghosts 90 bpm", index.Query(new SampleQuery { Sort = SampleSortKey.Duration, Descending = true }).First().Name);
        Assert.Equal(90.0, index.Query(new SampleQuery { Sort = SampleSortKey.Tempo, Descending = true, Filter = SampleSpecs.TempoBetween(0, 100) }).First().EffectiveTempoBpm);
        IReadOnlyList<Sample> byInstrument = index.Query(new SampleQuery { Sort = SampleSortKey.Instrument });
        Assert.Equal("808 Oracle 1", byInstrument[0].Name);
        Assert.Equal("Vinyl Dirt 1", byInstrument[^1].Name);
    }

    [Fact]
    public void Specifications_CombineWithAndOrNot()
    {
        SampleIndex index = Index();
        SampleSpecification shortMonoKick = SampleSpecs.InstrumentIs("kick") & SampleSpecs.Mono() & SampleSpecs.DurationBetween(0, 1.0);
        SampleSpecification kickOrSnare = SampleSpecs.InstrumentIs("kick") | SampleSpecs.InstrumentIs("snare");
        SampleSpecification notPercussion = !SampleSpecs.InstrumentIs("percussion");

        Assert.Equal(8, index.Query(new SampleQuery { Filter = shortMonoKick }).Count);
        Assert.Equal(6 + 6 + 2 + 1, index.Query(new SampleQuery { Filter = kickOrSnare }).Count);
        Assert.Equal(["808 Oracle 1", "Break Ghosts 90 bpm", "Groove B 180 bpm", "Vinyl Dirt 1"], index.Query(new SampleQuery { Filter = notPercussion }).Select(s => s.Name));
        Assert.Equal("((IsA(kick) AND Mono) AND Duration 0-1 s)", shortMonoKick.Description);
        Assert.Equal("NOT IsA(percussion)", notPercussion.ToString());
    }

    [Fact]
    public void TempoBand_IsHalfOpenSoNeighbouringBandsNeverShareASample()
    {
        SampleIndex index = new([TestSamples.Make("Just under", tempoBpm: 99.9), TestSamples.Make("On the edge", tempoBpm: 100), TestSamples.Make("Top of band", tempoBpm: 119.99), TestSamples.Make("Next band", tempoBpm: 120), TestSamples.Make("No tempo"), ]);

        Assert.Equal(["Just under"], index.Query(new SampleQuery { Filter = SampleSpecs.TempoBand(80, 100) }).Select(s => s.Name));
        Assert.Equal(["On the edge", "Top of band"], index.Query(new SampleQuery { Filter = SampleSpecs.TempoBand(100, 120) }).Select(s => s.Name));
        Assert.Equal(["Next band"], index.Query(new SampleQuery { Filter = SampleSpecs.TempoBand(120, double.PositiveInfinity) }).Select(s => s.Name));
    }

    [Fact]
    public void TempoBetween_UsesTheEffectiveTempo()
    {
        SampleIndex index = Index();

        Assert.Equal(["Break Ghosts 90 bpm"], index.Query(new SampleQuery { Filter = SampleSpecs.TempoBetween(85, 95) }).Select(s => s.Name));
        Assert.Equal(2, index.Query(new SampleQuery { Filter = SampleSpecs.TempoBetween(0, 300) }).Count);
    }

    [Fact]
    public void Text_MatchesNameInstrumentKitAndStyleWordsInAnyOrder()
    {
        SampleIndex index = Index();

        Assert.Equal(3, index.Query(new SampleQuery { Text = "909" }).Count);
        Assert.Equal(["Kick 909 DMX 1", "Kick 909 DMX 2"], index.Query(new SampleQuery { Text = "dmx kick" }).Select(s => s.Name));
        Assert.Equal(["Groove B 180 bpm"], index.Query(new SampleQuery { Text = "hip-hop" }).Select(s => s.Name));
        Assert.Equal(["808 Oracle 1"], index.Query(new SampleQuery { Text = "808 bass" }).Select(s => s.Name));
        Assert.Empty(index.Query(new SampleQuery { Text = "zzzz" }));
        Assert.Equal(index.Count, index.Query(new SampleQuery { Text = "  " }).Count);
    }
    #endregion
}
