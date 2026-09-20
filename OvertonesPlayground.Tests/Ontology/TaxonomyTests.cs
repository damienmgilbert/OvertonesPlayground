namespace OvertonesPlayground.Tests.Ontology;

public sealed class TaxonomyTests
{
    #region Structure
    [Fact]
    public void Instruments_Load_WithACategoryFamilyAndRefinementHierarchy()
    {
        Taxonomy<InstrumentConcept> taxonomy = Taxonomies.Instruments;

        Assert.Equal("instrument", taxonomy.Root.Key);
        Assert.True(taxonomy.All.Count > 70, $"only {taxonomy.All.Count} instrument concepts");
        Assert.Contains(taxonomy.All, concept => concept.Key == "unclassified");
        Assert.Equal(0, taxonomy.Root.Depth);
        Assert.Equal(1, taxonomy.Get("percussion").Depth);
        Assert.Equal(2, taxonomy.Get("hihat").Depth);
        Assert.Equal(3, taxonomy.Get("hihat-closed").Depth);
    }

    [Theory]
    [InlineData("instrument")]
    [InlineData("kit")]
    [InlineData("style")]
    public void Roots_HaveNoParent_AndEveryOtherConceptDoes(string rootKey)
    {
        IReadOnlyList<OntologyConcept> all = rootKey switch
        {
            "instrument" => Taxonomies.Instruments.All,
            "kit" => Taxonomies.Kits.All,
            _ => Taxonomies.Styles.All,
        };

        Assert.Null(all[0].Parent);
        Assert.Equal(rootKey, all[0].Key);
        Assert.All(all.Skip(1), concept => Assert.NotNull(concept.Parent));
        Assert.All(all, concept => Assert.True(concept.Depth < 10, $"{concept.Key} is suspiciously deep"));
    }

    [Fact]
    public void Aliases_AreUniqueWithinEachTaxonomy()
    {
        AssertNoAliasIsSharedBetweenConcepts(Taxonomies.Instruments);
        AssertNoAliasIsSharedBetweenConcepts(Taxonomies.Kits);
        AssertNoAliasIsSharedBetweenConcepts(Taxonomies.Styles);
    }

    private static void AssertNoAliasIsSharedBetweenConcepts<T>(Taxonomy<T> taxonomy)
        where T : OntologyConcept
    {
        IEnumerable<IGrouping<string, ConceptAlias<T>>> byPhrase = taxonomy.Aliases.GroupBy(alias => (alias.StartOnly ? "^" : string.Empty) + string.Join(' ', alias.Tokens));
        foreach (IGrouping<string, ConceptAlias<T>> group in byPhrase)
        {
            string[] owners = [.. group.Select(alias => alias.Concept.Key).Distinct()];
            Assert.True(owners.Length == 1, $"alias '{group.Key}' names {string.Join(" and ", owners)}");
        }
    }
    #endregion

    #region Is-a relations
    [Fact]
    public void IsA_IsTrueForSelfAndAncestorsOnly()
    {
        InstrumentConcept closed = Taxonomies.Instruments.Get("hihat-closed");

        Assert.True(closed.IsA("hihat-closed"));
        Assert.True(closed.IsA("hihat"));
        Assert.True(closed.IsA("percussion"));
        Assert.True(closed.IsA(Taxonomies.Instruments.Root));
        Assert.False(closed.IsA("cymbal"));
        Assert.False(closed.IsA("hihat-open"));
        Assert.False(Taxonomies.Instruments.Get("hihat").IsA("hihat-closed"));
    }

    [Fact]
    public void FamilyAndCategory_ResolveToDepthTwoAndOne()
    {
        InstrumentConcept closed = Taxonomies.Instruments.Get("hihat-closed");
        InstrumentConcept kick = Taxonomies.Instruments.Get("kick");
        InstrumentConcept sfx = Taxonomies.Instruments.Get("sfx");

        Assert.Equal("hihat", closed.Family.Key);
        Assert.Equal("percussion", closed.Category.Key);
        Assert.Equal("kick", kick.Family.Key);
        Assert.Equal("sfx", sfx.Family.Key);
        Assert.Equal("Percussion > Hi-Hat > Closed Hi-Hat", closed.Path);
    }

    [Fact]
    public void DescendantsAndSelf_ListsTheWholeSubtreeDepthFirst()
    {
        string[] keys = [.. Taxonomies.Instruments.Get("hihat").DescendantsAndSelf().Select(concept => concept.Key)];

        Assert.Equal(["hihat", "hihat-closed", "hihat-open", "hihat-pedal"], keys);
    }
    #endregion

    #region Inherited data
    [Theory]
    [InlineData("kick", ContentType.OneShot)]
    [InlineData("hihat-open", ContentType.OneShot)]
    [InlineData("impact", ContentType.Transition)]
    [InlineData("riser", ContentType.Transition)]
    [InlineData("atmosphere", ContentType.Texture)]
    [InlineData("stab", ContentType.Stab)]
    [InlineData("drum-loop", ContentType.Loop)]
    public void DefaultContentType_IsInheritedFromTheNearestAncestorThatSetsIt(string key, ContentType expected)
    {
        Assert.Equal(expected, Taxonomies.Instruments.Get(key).DefaultContentType);
    }

    [Theory]
    [InlineData("kick", true)]
    [InlineData("hihat-closed", true)]
    [InlineData("crash", true)]
    [InlineData("foley", false)]
    [InlineData("electronic-percussion", false)]
    [InlineData("stab", false)]
    [InlineData("vocal", false)]
    public void IsDistinctive_MarksOnlyFamiliesWithAWellDefinedSound(string key, bool expected)
    {
        Assert.Equal(expected, Taxonomies.Instruments.Get(key).IsDistinctive);
    }

    [Theory]
    [InlineData("808", SoundOrigin.Analog)]
    [InlineData("909", SoundOrigin.DrumMachine)]
    [InlineData("mpc", SoundOrigin.Sampled)]
    [InlineData("vinyl", SoundOrigin.Sampled)]
    [InlineData("taka", SoundOrigin.Unknown)]
    public void KitOrigin_IsInheritedFromTheNearestAncestorThatSetsIt(string key, SoundOrigin expected)
    {
        Assert.Equal(expected, Taxonomies.Kits.Get(key).Origin);
    }
    #endregion

    #region Lookup
    [Fact]
    public void Get_UnknownKey_Throws()
    {
        _ = Assert.Throws<KeyNotFoundException>(() => Taxonomies.Instruments.Get("no-such-instrument"));
        Assert.False(Taxonomies.Instruments.TryGet("no-such-instrument", out _));
    }

    [Fact]
    public void ChildrenOf_ReturnsTypedDirectChildren()
    {
        IReadOnlyList<InstrumentConcept> children = Taxonomies.Instruments.ChildrenOf(Taxonomies.Instruments.Get("cymbal"));

        Assert.Equal(["crash", "ride", "splash", "china"], children.Select(concept => concept.Key));
    }

    [Fact]
    public void Aliases_IncludeTheKeyAndDisplayNameOfEveryConcept()
    {
        Assert.Contains(Taxonomies.Instruments.Aliases, alias => alias.Concept.Key == "hihat-closed" && alias.Tokens.SequenceEqual(["hihat", "closed"]));
        Assert.Contains(Taxonomies.Instruments.Aliases, alias => alias.Concept.Key == "e-piano" && alias.Tokens.SequenceEqual(["e", "piano"]));
    }

    [Fact]
    public void StartOnlyAliases_AreMarked()
    {
        Assert.Contains(Taxonomies.Instruments.Aliases, alias => alias.Concept.Key == "bass-808" && alias.StartOnly && alias.Tokens.SequenceEqual(["808"]));
    }
    #endregion

    #region Text normalizer and lexicon
    [Theory]
    [InlineData("E-Perc Blip EDM", new[] { "e", "perc", "blip", "edm" })]
    [InlineData("Break Ahmir's Voodoo", new[] { "break", "ahmirs", "voodoo" })]
    [InlineData("Bass Sub C#0", new[] { "bass", "sub", "c#0" })]
    [InlineData("  Kick_909  DMX  ", new[] { "kick", "909", "dmx" })]
    [InlineData("", new string[0])]
    public void Tokenize_LowerCasesAndSplitsOnSeparators(string text, string[] expected)
    {
        Assert.Equal(expected, TextNormalizer.Tokenize(text));
    }

    [Fact]
    public void Lexicon_MapsKeywordsToContentTypeAndOrigin()
    {
        Lexicon lexicon = Taxonomies.Lexicon;

        Assert.Contains("riser", lexicon.ContentTypeKeywords[ContentType.Transition]);
        Assert.Contains("atmos", lexicon.ContentTypeKeywords[ContentType.Texture]);
        Assert.Contains("vinyl", lexicon.OriginKeywords[SoundOrigin.Sampled]);
        Assert.Contains("and", lexicon.IgnoredTokens);
    }
    #endregion

    #region Musical notes
    [Theory]
    [InlineData(440.0, 69.0)]
    [InlineData(261.6256, 60.0)]
    [InlineData(220.0, 57.0)]
    public void MidiOf_ConvertsFrequencyToNoteNumber(double hz, double midi)
    {
        Assert.Equal(midi, MusicalNotes.MidiOf(hz), 0.01);
        Assert.Equal(hz, MusicalNotes.FrequencyOf(midi), hz * 0.001);
    }

    [Theory]
    [InlineData(60, "C4")]
    [InlineData(61, "C#4")]
    [InlineData(57, "A3")]
    [InlineData(28, "E1")]
    public void NoteName_UsesScientificPitchNotation(int midi, string expected)
    {
        Assert.Equal(expected, MusicalNotes.NoteName(midi));
    }

    [Theory]
    [InlineData("C", 0, true)]
    [InlineData("F#", 6, true)]
    [InlineData("Bb", 10, true)]
    [InlineData("H", 0, false)]
    [InlineData("C##", 0, false)]
    public void TryParsePitchClass_AcceptsNaturalsSharpsAndFlats(string text, int expected, bool succeeds)
    {
        bool result = MusicalNotes.TryParsePitchClass(text, out int pitchClass);

        Assert.Equal(succeeds, result);
        if (succeeds)
        {
            Assert.Equal(expected, pitchClass);
        }
    }
    #endregion
}
