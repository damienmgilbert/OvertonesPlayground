# OvertonesPlayground.Ontology

Purpose
-------

OvertonesPlayground.Ontology provides the sample analysis, classification and runtime indexing used by the OvertonesPlayground app.
It decodes bundled WAV assets, extracts acoustic descriptors (facets) such as spectral / tonal / dynamic / rhythm features, classifies samples
(instrument, kit, style, content type and origin) from filename, metadata and audio, and produces a precomputed sample catalog consumed by the app.

This project is designed for offline precomputation (the SampleAnalyzer tool) and fast in-memory runtime lookups (SampleIndex).

Key components
--------------

- Analysis
  - SampleAnalysisPipeline: top-level pipeline to turn a WAV file into a Sample with all acoustic facets.
  - RiffWavReader, WavData, WavFormat, RiffMetadata: tolerant WAV decoding and embedded metadata extraction.
  - Analysis/Features: SpectralAnalyzer, TonalityAnalyzer, RhythmAnalyzer, DynamicsAnalyzer, StereoAnalyzer, TechnicalAnalyzer (feature extractors).
  - Analysis/Dsp: STFT, YIN pitch detector, decimator, filters and helper DSP utilities.

- Classification
  - CorpusClassifier: two-pass classifier (name/metadata pass → train signal classifier → fusion pass).
  - FilenameParser, FilenameLexiconClassifier, MetadataClassifier, SignalHeuristicClassifier, EvidenceFusion.
  - ClassificationOverrides / overrides.json: manual corrections applied last.

- Catalog & Serialization
  - SampleCatalog: precomputed description of every bundled sample (schema and analyzer versioned).
  - SampleCatalogSerializer & SampleCatalogJsonContext: trim-safe JSON read/write; serialized as one-sample-per-line for reviewable diffs.

- Querying & Indexing
  - SampleIndex: in-memory read-only index over the catalog supporting id lookup, free-text search, specification filtering, similarity search (fingerprint space), kits/variations/tempo/key groups and relationship discovery.
  - SampleSpecification / SampleSpecs: composable predicates used to filter samples.

- Concepts & Data
  - Taxonomies loads Data/taxonomy.json (instruments, kits, styles) and Data/lexicon.json (keyword sets) as embedded resources.
  - Lexicon and concept trees drive the filename and metadata classifiers.

- Theory (music theory)
  - PitchClass and Interval: notes of the octave, spelled with sharps or flats.
  - Scale / Scales: major, minor, pentatonic, blues, chromatic and the modes. The order of Scales.All is fixed, because the Launchpad stores a scale by its position.
  - Chord and ChordQuality: triads to elevenths, parsed from lead-sheet symbols and from sample names ("E-Piano LDre BbMaj7").
  - Key: a tonic and a scale, with its diatonic chords and relative key.
  - RomanNumeral and Progression: "i-VI-III-VII" played in any key.
  - Meter and StepPattern: the X x o - ? . step notation, measures of a groove (density, syncopation) and a drummer's variations (ghost notes, nudges, thinning, fills).
  - KeyCompatibility: whether a sample can sound in a key. It judges chords by their triad, notes by their pitch and loops strictly by their key, and treats drums as neutral.

- Styles
  - StyleProfiles loads Data/styles.json: 15 styles. Each has a tempo and swing range, scales, progressions, suitable kits, a pattern library for each part, and ready-made presets. The Launchpad's project generator follows these profiles, and tools/ProjectForge proposes the presets.

- Recommendation
  - SampleRecommender (in Querying): suggests samples for a slot of a project. It keeps to the slot's instrument, the key and the tempo, prefers the project's kits and character, avoids sub-bass masking and near copies, and gives the reasons in words. It also infers a project's key (InferKey) and tempo (InferTempo) from its samples.

Quick start
-----------

1) Produce a catalog from WAV files

```csharp
using OvertonesPlayground.Ontology.Analysis;
using OvertonesPlayground.Ontology.Catalog;

var pipeline = new SampleAnalysisPipeline();
var samples = Directory.EnumerateFiles("samples", "*.wav")
	.Select(f => pipeline.AnalyzeFile(f))
	.ToList();

var catalog = SampleCatalog.Create(samples);
SampleCatalogSerializer.Save(catalog, "sample-catalog.json");
```

2) Load catalog and build runtime index

```csharp
using OvertonesPlayground.Ontology.Catalog;
using OvertonesPlayground.Ontology.Querying;

var catalog = SampleCatalogSerializer.Deserialize(File.ReadAllText("sample-catalog.json"));
var index = new SampleIndex(catalog.Samples);

// Free-text search
var results = index.Query(new SampleQuery { Text = "kick", Limit = 50 });

// Find acoustically similar items
var similar = index.FindSimilar(results.FirstOrDefault()!, 10);
```

3) Classify a corpus (two-pass)

```csharp
using OvertonesPlayground.Ontology.Classification;

var overrides = ClassificationOverrides.Load("overrides.json");
var classifier = new CorpusClassifier(overrides);
var classified = classifier.Classify(catalog.Samples);
```

Extending the ontology
----------------------

- Update concept trees or keywords: edit `Data/taxonomy.json` and `Data/lexicon.json` and rebuild. These files are embedded resources and are the canonical source for instruments, kits, styles and token lexicons.
- Re-run the analyzer (tools/SampleAnalyzer or SampleAnalysisPipeline) to regenerate `sample-catalog.json` after changing taxonomies or adding samples.
- Use `overrides.json` to make manual corrections without modifying the lexicon.

Notes and implementation details
--------------------------------

- The project is intentionally conservative in classification: when evidence disagrees or confidence is low, samples are flagged for human review instead of being silently mislabelled.
- SampleCatalog includes schema and analyzer version constants (SampleCatalog.CurrentSchemaVersion and CurrentAnalyzerVersion); the app uses these to detect stale catalogs.
- Sound-family clustering and similarity use an internal fingerprint space computed from acoustic features; SampleIndex computes clusters lazily and caches results for fast runtime queries.

Where to look next
------------------
- Analysis/Features/* — feature extraction implementations
- Classification/* — classifiers, fusion and overrides
- Querying/SampleIndex.cs — runtime search and similarity APIs
- Catalog/* — JSON contexts and serializer
- Data/taxonomy.json, Data/lexicon.json — editable sources for concepts and keywords