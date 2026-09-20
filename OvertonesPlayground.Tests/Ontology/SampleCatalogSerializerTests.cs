using System.Text;

namespace OvertonesPlayground.Tests.Ontology;

public sealed class SampleCatalogSerializerTests
{
    private static SampleCatalog Catalog() => SampleCatalog.Create(
    [
        TestSamples.Make("Kick 909 DMX 1", "kick", kit: "909", origin: SoundOrigin.DrumMachine, fundamentalHz: 55.0, tempoBpm: 120, keyPitchClass: 1, stem: "kick 909 dmx", variation: 1, needsReview: true),
        TestSamples.Make("Break Ghosts 90 bpm", "drum-loop", image: StereoImage.Wide, content: ContentType.Break, style: "hip-hop", durationSeconds: 5.3),
    ]);

    private static string Serialize(SampleCatalog catalog)
    {
        using MemoryStream stream = new();
        SampleCatalogSerializer.Serialize(catalog, stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    [Fact]
    public void RoundTrip_PreservesFacetsLabelsAndEnums()
    {
        SampleCatalog original = Catalog();

        SampleCatalog copy = SampleCatalogSerializer.Deserialize(Serialize(original));

        Assert.Equal(original.SchemaVersion, copy.SchemaVersion);
        Assert.Equal(original.AnalyzerVersion, copy.AnalyzerVersion);
        Assert.Equal(original.Samples.Count, copy.Samples.Count);
        for (int i = 0; i < original.Samples.Count; i++)
        {
            Sample a = original.Samples[i];
            Sample b = copy.Samples[i];
            Assert.Equal(a.Asset, b.Asset);
            Assert.Equal(a.Status, b.Status);
            Assert.Equal(a.Technical, b.Technical);
            Assert.Equal(a.Tonality, b.Tonality);
            Assert.Equal(a.Stereo, b.Stereo);
            Assert.Equal(a.Dynamics, b.Dynamics);
            Assert.Equal(a.Spectral!.Bands, b.Spectral!.Bands);
            Assert.Equal(a.Spectral.Mfcc, b.Spectral.Mfcc);
            Assert.Equal(a.Classification.Instrument.Value, b.Classification.Instrument.Value);
            Assert.Equal(a.Classification.Kit?.Value, b.Classification.Kit?.Value);
            Assert.Equal(a.Classification.ContentType.Value, b.Classification.ContentType.Value);
            Assert.Equal(a.Classification.Origin.Value, b.Classification.Origin.Value);
            Assert.Equal(a.Classification.Attributes, b.Classification.Attributes);
            Assert.Equal(a.Classification.NeedsReview, b.Classification.NeedsReview);
            Assert.Equal(a.Classification.ReviewReasons, b.Classification.ReviewReasons);
            Assert.Equal(a.Classification.Instrument.Evidence, b.Classification.Instrument.Evidence);
        }
    }

    [Fact]
    public void Serialize_WritesEnumsAsNamesAndFlagsAsCommaSeparatedNames()
    {
        string json = Serialize(Catalog());

        Assert.Contains("\"image\":\"Wide\"", json);
        Assert.Contains("\"shape\":\"Impulsive\"", json);
        Assert.Contains("\"contentType\":{\"value\":\"Break\"", json);
        Assert.Contains("\"character\":\"Pitched\"", json);
    }

    [Fact]
    public void Serialize_WritesOneSamplePerLine_AndOmitsComputedProperties()
    {
        string[] lines = Serialize(Catalog()).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(4, lines.Length);
        Assert.StartsWith("{\"schemaVersion\":1", lines[0], StringComparison.Ordinal);
        Assert.Contains("\"Break Ghosts 90 bpm.wav\"", lines[1]);
        Assert.Contains("\"Kick 909 DMX 1.wav\"", lines[2]);
        Assert.DoesNotContain("displayName", string.Concat(lines));
        Assert.DoesNotContain("\"id\":", string.Concat(lines));
        Assert.DoesNotContain("effectiveTempoBpm", string.Concat(lines));
        Assert.DoesNotContain("loudness\":", string.Concat(lines));
    }

    [Fact]
    public void Serialize_IsDeterministic()
    {
        Assert.Equal(Serialize(Catalog()), Serialize(Catalog()));
    }

    [Fact]
    public void Create_OrdersSamplesByFileName()
    {
        SampleCatalog catalog = SampleCatalog.Create([TestSamples.Make("b"), TestSamples.Make("A"), TestSamples.Make("c")]);

        Assert.Equal(["A.wav", "b.wav", "c.wav"], catalog.Samples.Select(s => s.Id));
        Assert.Equal(SampleCatalog.CurrentSchemaVersion, catalog.SchemaVersion);
        Assert.Equal(SampleCatalog.CurrentAnalyzerVersion, catalog.AnalyzerVersion);
    }

    [Fact]
    public void Deserialize_WrongSchemaVersion_Throws()
    {
        string json = Serialize(Catalog()).Replace("\"schemaVersion\":1", "\"schemaVersion\":99", StringComparison.Ordinal);

        _ = Assert.Throws<InvalidDataException>(() => SampleCatalogSerializer.Deserialize(json));
    }

    [Fact]
    public void Deserialize_ANonSeekableStream_Works()
    {
        using MemoryStream source = new(Encoding.UTF8.GetBytes(Serialize(Catalog())));
        using NonSeekableStream stream = new(source);

        SampleCatalog catalog = SampleCatalogSerializer.Deserialize(stream);

        Assert.Equal(2, catalog.Samples.Count);
    }

    [Fact]
    public void Save_CreatesTheFolderAndWritesAReadableFile()
    {
        string folder = Path.Combine(Path.GetTempPath(), "catalog-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "nested", "catalog.json");
        try
        {
            SampleCatalogSerializer.Save(Catalog(), path);

            using FileStream stream = File.OpenRead(path);
            Assert.Equal(2, SampleCatalogSerializer.Deserialize(stream).Samples.Count);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    [Fact]
    public void Samples_AreImmutableRecords_SoWithProducesACopy()
    {
        Sample original = TestSamples.Make("Kick");

        Sample changed = original with { Status = AnalysisStatus.Partial };

        Assert.Equal(AnalysisStatus.Analyzed, original.Status);
        Assert.Equal(AnalysisStatus.Partial, changed.Status);
        Assert.Equal(original.Asset, changed.Asset);
    }

    private sealed class NonSeekableStream(Stream inner) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
