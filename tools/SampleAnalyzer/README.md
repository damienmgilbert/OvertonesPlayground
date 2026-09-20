# SampleAnalyzer

Purpose
-------

SampleAnalyzer is a small command-line tool used to build and maintain the precomputed sample catalog consumed by the OvertonesPlayground app.
It decodes raw audio files, measures multiple acoustic facets, classifies the corpus (filename, metadata and audio evidence), and writes
sample-catalog.json. The tool also contains helpers to re-run classification, verify a catalog against the raw files, and print quality reports.

Key commands
------------

- analyze  — decode audio files, analyse each file, classify the corpus and write a new catalog.
  - Supports `--changed-only` to reuse unchanged catalogue entries (size + SHA-256) and only decode new/changed files.
  - Options: `--raw <dir>`, `--out <catalog.json>`, `--overrides <overrides.json>`, `--parallel <n>`, `--limit <n>`, `--changed-only`, `--catalog <catalog.json>`.

- classify — re-run classification on an existing catalog without decoding audio (useful when tuning lexicon or overrides).
  - Options: `--catalog <catalog.json>`, `--out <catalog.json>`, `--overrides <overrides.json>`.

- report   — print a data-quality report (coverage, disagreements between names and audio, per-family medians of facets).
  - Options: `--catalog <catalog.json>`, `--review <n>`.

- verify   — check that a catalog matches the raw files on disk (missing/extra/stale entries and analyzer version mismatches).
  - Options: `--catalog <catalog.json>`, `--raw <dir>`.

Defaults and repository layout
-------------------------------

When run without explicit paths the tool resolves defaults from the repository root:

- Raw audio folder: `OvertonesPlayground/Resources/Raw`
- Catalog path: `OvertonesPlayground/Resources/Raw/sample-catalog.json`
- Overrides: `tools/SampleAnalyzer/overrides.json`

Internals and behaviour
------------------------

- The `analyze` command uses `SampleAnalysisPipeline` to decode WAV/AIFF and extract facets (technical, spectral, tonality, dynamics, stereo, rhythm).
- Classification is a two-pass process implemented by `CorpusClassifier`: first use filename and metadata evidence, then train a signal classifier on confident items and fuse votes.
- `--changed-only` compares existing catalog entries to files by size and SHA-256 to avoid re-decoding unchanged files; the corpus is still re-classified in full so taxonomy/overrides updates apply.
- `report` prints summary statistics and runs a leave-one-out acoustic agreement evaluation (`CorpusClassifier.EvaluateSignalAgreement`) to highlight where names and audio disagree.
- `verify` reports missing/extra/stale files and detects when a catalog was built by a different analyzer version.

Practical examples
------------------

Analyze all audio with defaults:

```bash
dotnet run --project tools/SampleAnalyzer -- analyze
```

Analyze only new or changed files using an existing catalog:

```bash
dotnet run --project tools/SampleAnalyzer -- analyze --changed-only --catalog OvertonesPlayground/Resources/Raw/sample-catalog.json
```

Re-classify an existing catalog (no audio decoded):

```bash
dotnet run --project tools/SampleAnalyzer -- classify --catalog OvertonesPlayground/Resources/Raw/sample-catalog.json --out sample-catalog.reclassed.json
```

Print a report to inspect coverage and disagreements:

```bash
dotnet run --project tools/SampleAnalyzer -- report --catalog OvertonesPlayground/Resources/Raw/sample-catalog.json --review 40
```

Verify the catalog matches the raw folder:

```bash
dotnet run --project tools/SampleAnalyzer -- verify --catalog OvertonesPlayground/Resources/Raw/sample-catalog.json --raw OvertonesPlayground/Resources/Raw
```

Overrides and configuration
---------------------------

- `tools/SampleAnalyzer/overrides.json` is the default manual override file the tool applies when classifying. An override entry can set instrument, kit, style, origin or content type per file.
- Taxonomy and lexicon sources live in `OvertonesPlayground.Ontology/Data/taxonomy.json` and `OvertonesPlayground.Ontology/Data/lexicon.json` and are embedded by the Ontology library; edit those and rebuild to change the concept trees or keyword sets.

Recommendations
---------------

- Run `analyze` to produce or update the canonical `sample-catalog.json` and commit the resulting file as the app asset.
- After editing taxonomies or the overrides file, run `classify` (or `analyze`) and use `report` to inspect the effect and `verify` to ensure consistency.