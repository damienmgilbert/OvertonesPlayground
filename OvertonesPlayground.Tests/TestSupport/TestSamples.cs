namespace OvertonesPlayground.Tests.TestSupport;

///<summary>
///Builds fully populated <see cref="Sample"/> values for query, classification and catalog tests without touching audio.
///Each parameter sets the one property a test cares about; everything else is a sensible default.
///</summary>
public static class TestSamples
{
    #region Public methods
    ///<summary>A sample with all facets. <paramref name="name"/> is the display name; <c>.wav</c> is appended for the id.</summary>
    public static Sample Make(
        string name,
        string instrument = "kick",
        double instrumentConfidence = 0.95,
        double centroidHz = 100,
        double flatness = 0.05,
        double durationSeconds = 0.5,
        double lufs = -14.0,
        StereoImage image = StereoImage.Mono,
        EnvelopeShape shape = EnvelopeShape.Impulsive,
        TonalCharacter character = TonalCharacter.Pitched,
        double? fundamentalHz = null,
        double? tempoBpm = null,
        int? keyPitchClass = null,
        string stem = "",
        int? variation = null,
        string? kit = null,
        string? style = null,
        ContentType content = ContentType.OneShot,
        SoundOrigin origin = SoundOrigin.Unknown,
        bool needsReview = false,
        double attackMs = 2.0,
        double decayMs = 150.0)
    {
        SampleAsset asset = new(name + ".wav", 1000, "hash-" + name);
        double? midi = fundamentalHz is null ? null : MusicalNotes.MidiOf(fundamentalHz.Value);
        double[] bands = new double[FrequencyBands.Count];
        bands[(int)FrequencyBands.FromFrequency(centroidHz)] = 1.0;

        SampleClassification classification = new(
            new Label<string>(instrument, instrumentConfidence, [new Evidence(EvidenceSource.Filename, "test")]),
            [],
            new Label<ContentType>(content, 0.9, []),
            new Label<SoundOrigin>(origin, origin == SoundOrigin.Unknown ? 0.0 : 0.7, []),
            kit is null ? null : new Label<string>(kit, 0.9, []),
            style is null ? null : new Label<string>(style, 0.75, []),
            new NamedAttributes(tempoBpm, keyPitchClass, KeyMode.None, null, variation, string.IsNullOrEmpty(stem) ? name.ToLowerInvariant() : stem),
            needsReview,
            needsReview ? ["test review"] : []);

        return new Sample(
            asset,
            AnalysisStatus.Analyzed,
            new TechnicalFacet(44100, 24, image == StereoImage.Mono ? 1 : 2, durationSeconds, 1000, "PCM", false, 0, null, null),
            new SpectralFacet(centroidHz, centroidHz * 1.5, centroidHz * 0.5, flatness, 0.1, -6, BandEnergies.FromArray(bands), centroidHz * 1.2, new double[12]),
            new TonalityFacet(fundamentalHz, midi, fundamentalHz is null ? 0.2 : 0.95, fundamentalHz is null ? -5 : 25, 0.0, character),
            new DynamicsFacet(-1, -1, lufs - 3, lufs - 3, lufs, false, 0, 12, 0, 0, 0, 0, durationSeconds, attackMs, decayMs, shape),
            new StereoFacet(image == StereoImage.Mono ? 1 : 2, image, image == StereoImage.Wide ? 0.2 : 1.0, 1.0, image == StereoImage.Wide ? 0.4 : 0.0, 0, 0, 0, false),
            null,
            classification);
    }

    ///<summary>
    ///A small mixed library: the drum kit plus an 808 bass, two loops, a texture and 909 variations, so filters, kits, tempo and
    ///stereo all have something to find.
    ///</summary>
    public static List<Sample> Library() =>
    [
        .. DrumKit(6),
        Make("808 Oracle 1", "bass-808", centroidHz: 45, durationSeconds: 2.5, fundamentalHz: 41.2, kit: "808", keyPitchClass: 4, lufs: -9, character: TonalCharacter.Pitched | TonalCharacter.Sub),
        Make("Break Ghosts 90 bpm", "drum-loop", centroidHz: 900, flatness: 0.5, durationSeconds: 5.3, tempoBpm: 90, content: ContentType.Break, image: StereoImage.Wide, lufs: -20),
        Make("Groove B 180 bpm", "drum-loop", centroidHz: 950, flatness: 0.5, durationSeconds: 5.0, tempoBpm: 180, content: ContentType.Loop, style: "hip-hop"),
        Make("Vinyl Dirt 1", "noise", centroidHz: 5300, flatness: 0.78, durationSeconds: 4.8, content: ContentType.Texture, image: StereoImage.DualMono, lufs: -30),
        Make("Kick 909 DMX 1", "kick", centroidHz: 70, kit: "909", origin: SoundOrigin.DrumMachine, stem: "kick 909 dmx", variation: 1),
        Make("Kick 909 DMX 2", "kick", centroidHz: 75, kit: "909", origin: SoundOrigin.DrumMachine, stem: "kick 909 dmx", variation: 2),
        Make("Snare 909 DMX 1", "snare", centroidHz: 1900, kit: "909", stem: "snare 909 dmx", variation: 1),
    ];

    ///<summary>A small library of kicks, snares and hats with tight acoustic clusters, for signal-classification tests.</summary>
    public static List<Sample> DrumKit(int perFamily = 12)
    {
        List<Sample> samples = [];
        for (int i = 0; i < perFamily; i++)
        {
            double wobble = i * 0.01;
            samples.Add(Make($"Kick Test {i + 1}", "kick", centroidHz: 80 + (i * 2), flatness: 0.04 + wobble, durationSeconds: 0.5, decayMs: 250, fundamentalHz: 55));
            samples.Add(Make($"Snare Test {i + 1}", "snare", centroidHz: 1800 + (i * 20), flatness: 0.40 + wobble, durationSeconds: 0.35, decayMs: 140, character: TonalCharacter.Unpitched | TonalCharacter.Noisy));
            samples.Add(Make($"Hihat Closed Test {i + 1}", "hihat-closed", centroidHz: 8000 + (i * 50), flatness: 0.62 + wobble, durationSeconds: 0.12, decayMs: 60, character: TonalCharacter.Unpitched | TonalCharacter.Noisy | TonalCharacter.Bright));
        }

        return samples;
    }
    #endregion
}
