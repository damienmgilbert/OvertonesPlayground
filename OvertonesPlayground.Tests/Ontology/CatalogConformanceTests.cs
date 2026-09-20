namespace OvertonesPlayground.Tests.Ontology;

///<summary>
///Checks the committed catalog (<c>Resources/Raw/sample-catalog.json</c>) against the audio files next to it. It reads
///the JSON and lists the folder; no audio is decoded, so it is fast enough to run on every build. If one of these fails
///after adding or changing samples, re-run <c>dotnet run --project tools/SampleAnalyzer -- analyze</c>.
///</summary>
public sealed class CatalogConformanceTests
{
    private static readonly Lazy<string> _rawDirectory = new(FindRawDirectory);
    private static readonly Lazy<SampleCatalog> _catalog = new(
                                                           () =>
                                                           {
                                                               using FileStream stream = File.OpenRead(Path.Combine(_rawDirectory.Value, "sample-catalog.json"));
                                                               return SampleCatalogSerializer.Deserialize(stream);
                                                           });
    #region Private methods
    private static IEnumerable<string> AudioFiles() => Directory.EnumerateFiles(_rawDirectory.Value).Where(path => new[] { ".wav", ".aif", ".aiff", ".aifc" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)).Select(Path.GetFileName).OfType<string>();

    private static string FindRawDirectory()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, "OvertonesPlayground", "Resources", "Raw");
            if (File.Exists(Path.Combine(directory.FullName, "OvertonesPlayground.slnx")) && Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException($"Could not find OvertonesPlayground/Resources/Raw above {AppContext.BaseDirectory}");
    }

    private static double Median(IEnumerable<double> values)
    {
        double[] sorted = [.. values.Order()];
        return sorted[sorted.Length / 2];
    }
    #endregion

    #region Public methods
    [Fact]
    public void AlmostEverySampleHasAnInstrument_AndEveryOneHasAContentType()
    {
        IReadOnlyList<Sample> samples = _catalog.Value.Samples;
        int unclassified = samples.Count(s => s.Classification.Instrument.Value == "unclassified");

        Assert.True(unclassified * 100.0 / samples.Count < 2.0, $"{unclassified} of {samples.Count} samples are unclassified");
        Assert.DoesNotContain(samples, s => s.Classification.ContentType.Value == ContentType.Unknown && s.Classification.Instrument.Value != "unclassified");
    }

    [Fact]
    public void Catalog_HasTheCurrentSchemaAndAnalyzerVersion()
    {
        Assert.Equal(SampleCatalog.CurrentSchemaVersion, _catalog.Value.SchemaVersion);
        Assert.Equal(SampleCatalog.CurrentAnalyzerVersion, _catalog.Value.AnalyzerVersion);
    }

    [Fact]
    public void EntriesAreUnique_AndSizesMatchTheFilesOnDisk()
    {
        Assert.Equal(_catalog.Value.Samples.Count, _catalog.Value.Samples.Select(s => s.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (Sample sample in _catalog.Value.Samples)
        {
            long onDisk = new FileInfo(Path.Combine(_rawDirectory.Value, sample.Id)).Length;
            Assert.True(onDisk == sample.Asset.SizeBytes, $"{sample.Id}: catalog says {sample.Asset.SizeBytes} bytes, file has {onDisk}");
        }
    }

    [Fact]
    public void EveryAnalysedSampleHasEveryFacetAndFiniteNumbers()
    {
        foreach (Sample sample in _catalog.Value.Samples.Where(s => s.Status == AnalysisStatus.Analyzed))
        {
            Assert.NotNull(sample.Technical);
            Assert.NotNull(sample.Spectral);
            Assert.NotNull(sample.Tonality);
            Assert.NotNull(sample.Dynamics);
            Assert.NotNull(sample.Stereo);

            double[] numbers = [sample.Spectral!.CentroidHz, sample.Spectral.RolloffHz, sample.Spectral.BandwidthHz, sample.Spectral.Flatness, sample.Spectral.Flux, sample.Spectral.TiltDbPerOctave, sample.Spectral.OnsetCentroidHz, .. sample.Spectral.Bands.ToArray(), .. sample.Spectral.Mfcc, sample.Dynamics!.PeakDb, sample.Dynamics.TruePeakDb, sample.Dynamics.RmsDb, sample.Dynamics.IntegratedLufs, sample.Dynamics.CrestDb, sample.Dynamics.AttackMs, sample.Dynamics.DecayMs, sample.Dynamics.ActiveSeconds, sample.Tonality!.PitchConfidence, sample.Tonality.HarmonicToNoiseDb, sample.Tonality.Inharmonicity, sample.Stereo!.Correlation, sample.Stereo.Width, sample.Stereo.MonoCompatibilityDb, sample.Technical!.DurationSeconds,];
            Assert.All(numbers, value => Assert.True(double.IsFinite(value), $"{sample.Id} has a non-finite value"));
            Assert.Equal(12, sample.Spectral.Mfcc.Length);
            Assert.Equal(1.0, sample.Spectral.Bands.ToArray().Sum(), 0.01);
        }
    }

    [Fact]
    public void EveryAudioFileHasACatalogEntry_AndEveryEntryHasAFile()
    {
        HashSet<string> files = new(AudioFiles(), StringComparer.OrdinalIgnoreCase);
        HashSet<string> entries = new(_catalog.Value.Samples.Select(sample => sample.Id), StringComparer.OrdinalIgnoreCase);

        Assert.Empty(files.Except(entries, StringComparer.OrdinalIgnoreCase).Order());
        Assert.Empty(entries.Except(files, StringComparer.OrdinalIgnoreCase).Order());
        Assert.True(files.Count >= 2000, $"expected the ~2,200 bundled samples, found {files.Count}");
    }

    [Fact]
    public void EveryTaxonomyKeyInTheCatalogExists()
    {
        foreach (Sample sample in _catalog.Value.Samples)
        {
            Assert.True(Taxonomies.Instruments.TryGet(sample.Classification.Instrument.Value, out _), $"{sample.Id}: unknown instrument '{sample.Classification.Instrument.Value}'");
            Assert.All(sample.Classification.AlternateInstruments, alt => Assert.True(Taxonomies.Instruments.TryGet(alt.Value, out _), $"{sample.Id}: unknown alternate '{alt.Value}'"));
            if (sample.Classification.Kit is not null)
            {
                Assert.True(Taxonomies.Kits.TryGet(sample.Classification.Kit.Value, out _), $"{sample.Id}: unknown kit '{sample.Classification.Kit.Value}'");
            }

            if (sample.Classification.Style is not null)
            {
                Assert.True(Taxonomies.Styles.TryGet(sample.Classification.Style.Value, out _), $"{sample.Id}: unknown style '{sample.Classification.Style.Value}'");
            }
        }
    }

    [Fact]
    public void KnownFileFormats_AreDescribedCorrectly()
    {
        IReadOnlyList<Sample> samples = _catalog.Value.Samples;

        Assert.Contains(samples, s => s.Technical is { BitsPerSample: 24, SampleRate: 96000, Channels: 1 });
        Assert.Contains(samples, s => s.Technical is { BitsPerSample: 16 });
        Assert.Contains(samples, s => s.Technical is { Encoding: "PCM-Extensible" });
        Assert.Contains(samples, s => s.Stereo is { Image: StereoImage.DualMono });
    }

    [Fact]
    public void NamedTemposAndKeys_AreRecorded()
    {
        IReadOnlyList<Sample> samples = _catalog.Value.Samples;

        Assert.True(samples.Count(s => s.Classification.Attributes.TempoBpm is not null) >= 100);
        Assert.True(samples.Count(s => s.Classification.Attributes.KeyPitchClass is not null) >= 100);
        Sample banjo = samples.Single(s => s.Id == "Banjo Arpeggio E Minor 100 bpm.wav");
        Assert.Equal(100.0, banjo.Classification.Attributes.TempoBpm);
        Assert.Equal("E min", banjo.Classification.Attributes.KeyName());
        Assert.Equal("banjo", banjo.Classification.Instrument.Value);
    }

    [Fact]
    public void ReviewFlagsAreRare_AndEveryFlagHasAReason()
    {
        IReadOnlyList<Sample> flagged = [.. _catalog.Value.Samples.Where(s => s.Classification.NeedsReview)];

        Assert.True(flagged.Count * 100.0 / _catalog.Value.Samples.Count < 5.0, $"{flagged.Count} samples are flagged for review");
        Assert.All(flagged, s => Assert.NotEmpty(s.Classification.ReviewReasons));
    }

    [Fact]
    public void TheCoreDrumFamilies_SoundTheWayTheirNamesSay()
    {
        IReadOnlyList<Sample> samples = _catalog.Value.Samples;
        IEnumerable<Sample> Family(string key) { return samples.Where(s => s.Spectral is not null && s.Classification.Instrument.Confidence >= 0.8 && Taxonomies.Instruments.Get(s.Classification.Instrument.Value).IsA(key)); }

        double kick = Median(Family("kick").Select(s => s.Spectral!.CentroidHz));
        double snare = Median(Family("snare").Select(s => s.Spectral!.CentroidHz));
        double hat = Median(Family("hihat").Select(s => s.Spectral!.CentroidHz));

        Assert.True(kick < snare && snare < hat, $"median centroids: kick {kick:0}, snare {snare:0}, hi-hat {hat:0} Hz");
        Assert.True(Median(Family("kick").Select(s => s.Spectral!.Flatness)) < Median(Family("hihat").Select(s => s.Spectral!.Flatness)));
        Assert.True(Median(Family("cymbal").Select(s => s.Technical!.DurationSeconds)) > Median(Family("hihat").Select(s => s.Technical!.DurationSeconds)));
        Assert.True(Family("tom").Count(s => s.Tonality!.Character.HasFlag(TonalCharacter.Pitched)) > Family("tom").Count() / 2);
        Assert.True(Family("hihat").Count(s => s.Tonality!.Character.HasFlag(TonalCharacter.Pitched)) < Family("hihat").Count() / 10);
    }

    [Fact]
    public void TheWholeCatalog_LoadsIntoAnIndexThatAnswersQueries()
    {
        SampleIndex index = new(_catalog.Value.Samples);

        Assert.Equal(_catalog.Value.Samples.Count, index.Count);
        Assert.True(index.Query(new SampleQuery { Filter = SampleSpecs.InstrumentIs("kick") & SampleSpecs.Mono() & SampleSpecs.Length(LengthClass.Short) }).Count > 10);
        Assert.NotEmpty(index.GetKits());
        Sample any = index.All[0];
        Assert.Equal(5, index.FindSimilar(any, 5).Count);
    }
    #endregion
}