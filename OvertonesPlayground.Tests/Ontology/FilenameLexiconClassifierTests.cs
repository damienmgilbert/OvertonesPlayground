namespace OvertonesPlayground.Tests.Ontology;

public sealed class FilenameLexiconClassifierTests
{
    private readonly FilenameLexiconClassifier _classifier = new();

    private IReadOnlyList<ClassificationProposal> Classify(string name)
    {
        Sample sample = TestSamples.Make(name);
        return _classifier.Classify(new ClassificationContext(FilenameParser.Parse(name), sample));
    }

    private string TopInstrument(string name) =>
        Classify(name).Where(p => p.Axis == ClassificationAxis.Instrument).OrderByDescending(p => p.Confidence).First().Value;

    [Theory]
    [InlineData("Hihat Closed Tamuz Half", "hihat-closed")]
    [InlineData("Hihat Open Trad", "hihat-open")]
    [InlineData("Kick 909 DMX 1", "kick")]
    [InlineData("Kick Aztec", "kick")]
    [InlineData("Snare Vinyl DLJ 2", "snare")]
    [InlineData("Tom Pluto Mop 1", "tom")]
    [InlineData("Clap Crunch", "clap")]
    [InlineData("Rim Sidestick Tamuz 1", "rim")]
    [InlineData("Ride Layered", "ride")]
    [InlineData("Crash Layered", "crash")]
    [InlineData("Cymbal 808 Ping", "cymbal")]
    [InlineData("Shaker 808 Reviv", "shaker")]
    [InlineData("Maracas 808", "shaker")]
    [InlineData("Conga Pete", "conga")]
    [InlineData("Cabassa SP", "cabasa")]
    [InlineData("Wood Tamuz 1", "woodblock")]
    [InlineData("E-Perc Blip EDM", "electronic-percussion")]
    [InlineData("Bell Ring ER Fenk 1", "bell")]
    [InlineData("E-Piano Chord Pan", "e-piano")]
    [InlineData("Harpsichord Pluck C2", "harpsichord")]
    [InlineData("Lyre Greek Picked E3", "lyre")]
    [InlineData("Banjo Arpeggio E Minor 100 bpm", "banjo")]
    [InlineData("Vocal Patience Main", "vocal")]
    [InlineData("Wavetable Pads", "synth-pad")]
    [InlineData("Forest Ambience", "atmosphere")]
    [InlineData("Cash Register", "foley")]
    [InlineData("Airhorn Verb", "airhorn")]
    public void Classify_RealFileNames_FindTheInstrument(string name, string expected)
    {
        Assert.Equal(expected, TopInstrument(name));
    }

    [Theory]
    [InlineData("808 Oracle 1", "bass-808")]
    [InlineData("Bass Sub C#0", "bass-sub")]
    [InlineData("Synth Bass Trampoline", "bass-synth")]
    [InlineData("Bass Electric Taka C", "bass-electric")]
    public void Classify_Bass_DistinguishesTheKinds(string name, string expected)
    {
        Assert.Equal(expected, TopInstrument(name));
    }

    [Fact]
    public void Classify_KickWithA808Kit_IsAKickNotABass()
    {
        IReadOnlyList<ClassificationProposal> proposals = Classify("Kick 808 Tone11");

        Assert.Equal("kick", TopInstrument("Kick 808 Tone11"));
        Assert.DoesNotContain(proposals, p => p.Axis == ClassificationAxis.Instrument && p.Value == "bass-808");
        Assert.Contains(proposals, p => p is { Axis: ClassificationAxis.Kit, Value: "808" });
    }

    [Fact]
    public void Classify_TheFirstWordIsTheMostConfident()
    {
        double first = Classify("Kick 909").First(p => p.Axis == ClassificationAxis.Instrument && p.Value == "kick").Confidence;
        double later = Classify("Vinyl Dust Kick").First(p => p.Axis == ClassificationAxis.Instrument && p.Value == "kick").Confidence;

        Assert.True(first > later);
        Assert.True(first > 0.9);
    }

    [Fact]
    public void Classify_TwoInstrumentsInOneName_KeepsTheSecondAsAnAlternate()
    {
        IReadOnlyList<ClassificationProposal> proposals = Classify("Snare Hat Combo Bangky");

        ClassificationProposal snare = proposals.First(p => p.Axis == ClassificationAxis.Instrument && p.Value == "snare");
        ClassificationProposal hat = proposals.First(p => p.Axis == ClassificationAxis.Instrument && p.Value == "hihat");
        Assert.True(snare.Confidence > hat.Confidence);
    }

    [Fact]
    public void Classify_NameWithABpmTagButNoInstrumentWord_IsAGenericLoop()
    {
        ClassificationProposal loop = Classify("Trap Tastic 140 bpm").First(p => p.Axis == ClassificationAxis.Instrument);

        Assert.Equal("mixed-loop", loop.Value);
        Assert.True(loop.Confidence >= 0.5);
    }

    [Fact]
    public void Classify_NameWithNoEvidenceAtAll_ProposesNoInstrument()
    {
        Assert.DoesNotContain(Classify("Kyathe Monks"), p => p.Axis == ClassificationAxis.Instrument);
    }

    [Theory]
    [InlineData("Kick 909 DMX 1", "909")]
    [InlineData("Snare Vinyl DLJ 2", "vinyl")]
    [InlineData("Tom DMX Fatso 1", "dmx")]
    [InlineData("Hihat Closed Tamuz Half", "tamuz")]
    [InlineData("Cabassa SP", "sp")]
    public void Classify_KitWords_AreFound(string name, string expected)
    {
        Assert.Equal(expected, Classify(name).First(p => p.Axis == ClassificationAxis.Kit).Value);
    }

    [Fact]
    public void Classify_KitWithAKnownOrigin_ProposesThatOrigin()
    {
        ClassificationProposal origin = Classify("Kick 909 DMX 1").First(p => p.Axis == ClassificationAxis.Origin);

        Assert.Equal(nameof(SoundOrigin.DrumMachine), origin.Value);
    }

    [Theory]
    [InlineData("Disco Future 110 bpm", "disco")]
    [InlineData("Trap Tastic 140 bpm", "trap")]
    [InlineData("Jazz Brushes 70 bpm", "jazz")]
    [InlineData("Garage Basic 141 bpm", "garage")]
    public void Classify_StyleWords_AreFound(string name, string expected)
    {
        Assert.Equal(expected, Classify(name).First(p => p.Axis == ClassificationAxis.Style).Value);
    }

    [Theory]
    [InlineData("Break Ghosts 90 bpm", ContentType.Break)]
    [InlineData("Riser Build Up", ContentType.Transition)]
    [InlineData("Atmos Calm 130 bpm", ContentType.Texture)]
    [InlineData("Stab Strings Cm", ContentType.Stab)]
    public void Classify_ContentTypeKeywords_ProposeTheContentType(string name, ContentType expected)
    {
        Assert.Contains(Classify(name), p => p.Axis == ClassificationAxis.ContentType && p.Value == expected.ToString() && p.Confidence >= 0.7);
    }

    [Fact]
    public void Classify_BpmTag_SuggestsALoop()
    {
        Assert.Contains(Classify("Conga 100 bpm"), p => p.Axis == ClassificationAxis.ContentType && p.Value == nameof(ContentType.Loop));
    }

    [Fact]
    public void Classify_PlainOneShot_DefaultsToOneShot()
    {
        Assert.Contains(Classify("Kick Aztec"), p => p.Axis == ClassificationAxis.ContentType && p.Value == nameof(ContentType.OneShot));
    }

    [Fact]
    public void Classify_EveryProposalCarriesFilenameEvidence()
    {
        Assert.All(Classify("Hihat Closed Tamuz Half"), p => Assert.Equal(EvidenceSource.Filename, p.Evidence.Source));
    }
}
