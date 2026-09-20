namespace OvertonesPlayground.Tests.Ontology;

public sealed class FilenameParserTests
{
    [Theory]
    [InlineData("Banjo Arpeggio E Minor 100 bpm", 100.0)]
    [InlineData("Outer Bongos 140bpm", 140.0)]
    [InlineData("80s Beat 90 bpm", 90.0)]
    [InlineData("Reptile F 130 bpm", 130.0)]
    public void Parse_BpmTag_GivesTempoWithOrWithoutASpace(string name, double expected)
    {
        Assert.Equal(expected, FilenameParser.Parse(name).Attributes.TempoBpm);
    }

    [Fact]
    public void Parse_NameWithoutBpm_HasNoTempo()
    {
        Assert.Null(FilenameParser.Parse("Hihat Closed Tamuz Half").Attributes.TempoBpm);
        Assert.Null(FilenameParser.Parse("Kick 909 DMX 1").Attributes.TempoBpm);
    }

    [Theory]
    [InlineData("Bass Sub C#0", 1, 0, KeyMode.None, "bass sub")]
    [InlineData("Bass Sine C1", 0, 1, KeyMode.None, "bass sine")]
    [InlineData("Lyre Greek Picked E3", 4, 3, KeyMode.None, "lyre greek picked")]
    [InlineData("Beefy Chop F#", 6, null, KeyMode.None, "beefy chop")]
    [InlineData("808 Heavy E", 4, null, KeyMode.None, "808 heavy")]
    [InlineData("Chugged Arp Fmin 130 bpm", 5, null, KeyMode.Minor, "chugged arp")]
    [InlineData("Banjo Arpeggio E Minor 100 bpm", 4, null, KeyMode.Minor, "banjo arpeggio")]
    [InlineData("Reptile F 130 bpm", 5, null, KeyMode.None, "reptile")]
    public void Parse_TrailingNoteOrKey_IsExtractedAndRemovedFromTheStem(string name, int pitchClass, int? octave, KeyMode mode, string stem)
    {
        NamedAttributes attributes = FilenameParser.Parse(name).Attributes;

        Assert.Equal(pitchClass, attributes.KeyPitchClass);
        Assert.Equal(octave, attributes.Octave);
        Assert.Equal(mode, attributes.KeyMode);
        Assert.Equal(stem, attributes.Stem);
    }

    [Theory]
    [InlineData("Kick A")]
    [InlineData("Tom B 1")]
    [InlineData("Kick 909 Tune1 d")]
    [InlineData("Snare 808 Tone1 k")]
    [InlineData("Hihat Closed Tamuz Half")]
    public void Parse_LettersThatAreNotNotes_AreNotTreatedAsKeys(string name)
    {
        Assert.Null(FilenameParser.Parse(name).Attributes.KeyPitchClass);
    }

    [Theory]
    [InlineData("Kick 909 DMX 1", 1, "kick 909 dmx")]
    [InlineData("808 Oracle 10", 10, "808 oracle")]
    [InlineData("Kick 808 Tone11", 11, "kick 808 tone")]
    [InlineData("Hihat Closed Plymouth 2", 2, "hihat closed plymouth")]
    public void Parse_TrailingNumber_IsTheVariationAndLeavesTheStemForGrouping(string name, int variation, string stem)
    {
        NamedAttributes attributes = FilenameParser.Parse(name).Attributes;

        Assert.Equal(variation, attributes.VariationNumber);
        Assert.Equal(stem, attributes.Stem);
    }

    [Theory]
    [InlineData("Tom ED10")]
    [InlineData("Tom R100")]
    [InlineData("Maracas 808")]
    [InlineData("Snare 909")]
    public void Parse_KitNames_AreNotMistakenForVariationNumbers(string name)
    {
        Assert.Null(FilenameParser.Parse(name).Attributes.VariationNumber);
    }

    [Fact]
    public void Parse_KeyAndVariationAndTempoTogether_AreAllExtracted()
    {
        NamedAttributes attributes = FilenameParser.Parse("Bass Sub C#0 2").Attributes;

        Assert.Equal(2, attributes.VariationNumber);
        Assert.Equal(1, attributes.KeyPitchClass);
        Assert.Equal("bass sub", attributes.Stem);
    }

    [Fact]
    public void Parse_Words_ExcludeTempoKeyAndVariation()
    {
        string[] words = FilenameParser.Parse("Banjo Arpeggio E Minor 100 bpm").Words;

        Assert.Equal(["banjo", "arpeggio"], words);
    }

    [Theory]
    [InlineData("Bass Sub C#0", "C#")]
    [InlineData("Chugged Arp Fmin 130 bpm", "F min")]
    [InlineData("Kick 909 DMX 1", null)]
    public void KeyName_FormatsTonicAndMode(string name, string? expected)
    {
        Assert.Equal(expected, FilenameParser.Parse(name).Attributes.KeyName());
    }

    [Fact]
    public void Parse_SingleWordName_IsLeftAlone()
    {
        ParsedName parsed = FilenameParser.Parse("Clap");

        Assert.Equal(["clap"], parsed.Words);
        Assert.Null(parsed.Attributes.KeyPitchClass);
    }
}
