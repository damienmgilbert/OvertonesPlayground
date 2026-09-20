namespace OvertonesPlayground.Tests.Ontology;

public sealed class SoundFamilyTests
{
    #region Private methods
    private static SampleIndex Index() => new(TestSamples.Library());
    #endregion

    #region Public methods
    [Fact]
    public void GetSoundFamilies_AFamilyIsNamedAfterItsDominantInstrumentAndItsMostTypicalSound()
    {
        SampleIndex index = Index();
        SoundFamily kicks = index.GetSoundFamily(index.All.Single(sample => sample.Name == "Kick Test 1"))!;

        Assert.Equal("kick", kicks.DominantInstrument!.Key);
        Assert.Equal($"Kick like {kicks.Medoid.Name}", kicks.Name);
        Assert.Contains(kicks.Medoid, kicks.Members);
        Assert.Equal("Family", kicks.Kind);
        Assert.InRange(kicks.Purity, 0.5, 1.0);
    }

    [Fact]
    public void GetSoundFamilies_AMixedFamilyIsNamedMixedRatherThanAfterAMinority()
    {
        // Two sounds with the same fingerprint but different instruments: neither reaches half of the family.
        Sample a = TestSamples.Make("A", "kick");
        Sample b = TestSamples.Make("B", "snare");
        Sample c = TestSamples.Make("C", "tom");
        SampleIndex index = new([a, b, c]);

        SoundFamily everything = Assert.Single(index.GetSoundFamilies());

        Assert.StartsWith("Mixed like ", everything.Name, StringComparison.Ordinal);
        Assert.Equal(1.0 / 3.0, everything.Purity, 1e-9);
    }

    [Fact]
    public void GetSoundFamilies_ArriveLargestFirst_WithMembersByName()
    {
        IReadOnlyList<SoundFamily> families = Index().GetSoundFamilies();

        Assert.Equal(families.OrderByDescending(family => family.Members.Count).Select(family => family.Members.Count), families.Select(family => family.Members.Count));
        foreach (SoundFamily family in families)
        {
            Assert.Equal(family.Members.Select(sample => sample.Name).Order(StringComparer.OrdinalIgnoreCase), family.Members.Select(sample => sample.Name));
        }
    }

    [Fact]
    public void GetSoundFamilies_ASoundWithNoAnalysis_BelongsToNoFamily()
    {
        Sample unanalysed = TestSamples.Make("Unreadable") with { Spectral = null };
        SampleIndex index = new([.. TestSamples.Library(), unanalysed]);

        Assert.Null(index.GetSoundFamily(unanalysed));
        Assert.Equal(index.Count - 1, index.GetSoundFamilies().Sum(family => family.Members.Count));
    }

    [Fact]
    public void GetSoundFamilies_ClosedAndOpenHatsCountAsOneFamilyOfHats()
    {
        Sample closed = TestSamples.Make("Closed", "hihat-closed");
        Sample open = TestSamples.Make("Open", "hihat-open");
        Sample other = TestSamples.Make("Other", "hihat");
        SampleIndex index = new([closed, open, other]);

        SoundFamily hats = Assert.Single(index.GetSoundFamilies());

        Assert.Equal("hihat", hats.DominantInstrument!.Key);
        Assert.Equal(1.0, hats.Purity);
    }

    [Fact]
    public void GetSoundFamilies_EverySampleBelongsToExactlyOneFamily()
    {
        SampleIndex index = Index();

        IReadOnlyList<SoundFamily> families = index.GetSoundFamilies();

        Assert.Equal(index.Count, families.Sum(family => family.Members.Count));
        Assert.Equal(index.Count, families.SelectMany(family => family.Members).Select(sample => sample.Id).Distinct().Count());
        foreach (Sample sample in index.All)
        {
            Assert.Contains(sample, index.GetSoundFamily(sample)!.Members);
        }
    }

    [Fact]
    public void GetSoundFamilies_NothingToCluster_IsEmpty()
    {
        SampleIndex index = new([]);

        Assert.Empty(index.GetSoundFamilies());
        Assert.Null(index.GetSoundFamily(TestSamples.Make("Nowhere")));
    }

    [Fact]
    public void GetSoundFamilies_OneSound_IsItsOwnFamily()
    {
        Sample only = TestSamples.Make("Only");
        SampleIndex index = new([only]);

        SoundFamily family = Assert.Single(index.GetSoundFamilies());

        Assert.Same(only, Assert.Single(family.Members));
        Assert.Same(only, family.Medoid);
        Assert.Equal(1.0, family.Purity);
    }

    [Fact]
    public void GetSoundFamilies_SoundsThatSoundAlikeLandTogetherAndDifferentOnesApart()
    {
        SampleIndex index = Index();
        SoundFamily FamilyOf(string name) { return index.GetSoundFamily(index.All.Single(sample => sample.Name == name))!; }

        Assert.All(Enumerable.Range(2, 5), i => Assert.Same(FamilyOf("Kick Test 1"), FamilyOf($"Kick Test {i}")));
        Assert.All(Enumerable.Range(2, 5), i => Assert.Same(FamilyOf("Hihat Closed Test 1"), FamilyOf($"Hihat Closed Test {i}")));
        Assert.NotSame(FamilyOf("Kick Test 1"), FamilyOf("Hihat Closed Test 1"));
        Assert.NotSame(FamilyOf("Kick Test 1"), FamilyOf("Snare Test 1"));
    }

    [Fact]
    public void GetSoundFamilies_ThePurityIsTheShareOfMembersInTheDominantInstrumentFamily()
    {
        SampleIndex index = Index();

        foreach (SoundFamily family in index.GetSoundFamilies())
        {
            int dominant = family.Members.Count(sample => Taxonomies.Instruments.Get(sample.Classification.Instrument.Value).Family.Key == family.DominantInstrument?.Key);
            Assert.Equal((double)dominant / family.Members.Count, family.Purity, 1e-9);
        }
    }

    [Fact]
    public void GetSoundFamilies_TheSameCatalogAlwaysGivesTheSameFamilies()
    {
        string[] Describe(SampleIndex index) { return[.. index.GetSoundFamilies().Select(family => $"{family.Name}: {string.Join(", ", family.Members.Select(sample => sample.Name))}")]; }

        Assert.Equal(Describe(Index()), Describe(Index()));
        Assert.Equal(Describe(Index()), Describe(new SampleIndex(TestSamples.Library().AsEnumerable().Reverse())));
    }
    #endregion
}
