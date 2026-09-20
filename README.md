# OvertonesPlayground

## The Sound Bank and the sample ontology

`OvertonesPlayground/Resources/Raw` holds about 2,200 bundled WAV samples (~970 MB). The **Sound Bank** page (flyout > *Sound
Bank*) lets you browse them by what they *are*, not just by file name: search, filter by instrument (drilling down the
taxonomy), kit, type, tempo, key, sound character, stereo image, length, loudness and envelope, sort by any acoustic facet,
audition a sound, find sounds that sound like it, and copy one into your library. The tempo row offers six BPM bands and the key
row the twelve pitch classes; both use the tempo or key written in the file name when there is one, and the detected value
otherwise (so only sounds with a tempo or a clear pitch appear under them).

That is driven by an ontology, in [`OvertonesPlayground.Ontology`](OvertonesPlayground.Ontology) (plain `net10.0`, no MAUI, so the
app, the tests and the analyzer tool all share it):

| Part | What it does |
| --- | --- |
| `Facets/` | One immutable record per acoustic category: `TechnicalFacet` (format), `TonalityFacet` (pitch, harmonicity, character flags), `SpectralFacet` (centroid, rolloff, flatness, seven Hz-defined bands, MFCCs), `DynamicsFacet` (peak, true peak, BS.1770 loudness, crest, clipping, attack/decay, envelope shape), `StereoFacet` (correlation, width, mono compatibility, phase), `RhythmFacet` (onsets, tempo, loop-ness). |
| `Concepts/` + `Data/taxonomy.json` | Is-a trees for instruments, kits and styles, loaded from JSON, not hard-coded. `Data/lexicon.json` holds the content-type and origin keywords. |
| `Classification/` | Classifiers vote (file-name lexicon, embedded metadata, and a nearest-neighbour vote on the audio itself); `EvidenceFusion` combines the votes, keeps every label's evidence and confidence, and flags disagreements for review instead of hiding them. `overrides.json` corrections are applied last. |
| `Querying/` | Composable specifications (`SampleSpecs.InstrumentIs("kick") & SampleSpecs.Mono()`), free-text search, sorting, similarity, relations, and collections (kits, variations, tempo and key groups, and *sound families*: k-means clusters of the acoustic fingerprint, found without looking at names), in `SampleIndex`. |
| `Analysis/` | A tolerant WAV reader (8/16/24/32-bit, float, `WAVE_FORMAT_EXTENSIBLE`, odd chunks, truncated data) and one analyzer per facet. |

The audio is analysed **once, on a PC**, into `Resources/Raw/sample-catalog.json` (about 4 MB, one sample per line so diffs stay
small). It ships as a `MauiAsset`, and the app only ever reads it; the tablet never decodes the audio to browse it.

### Regenerating the catalog

Run this after adding, removing or changing samples, editing `Data/taxonomy.json` / `Data/lexicon.json`, or changing an analyzer
(bump `SampleCatalog.CurrentAnalyzerVersion` when you change analysis or classification logic):

```bash
dotnet run --project tools/SampleAnalyzer -c Release -- analyze
```

| Command | Use it to |
| --- | --- |
| `analyze` | Decode every file, measure it, classify the corpus and write the catalog (about 5 s for the whole bank). |
| `analyze --changed-only` | Keep the samples of the existing catalog whose file is unchanged (same size and SHA-256), decode only new or changed files, drop entries whose file is gone, then classify the whole corpus again so overrides and taxonomy edits still apply. With nothing changed it takes about 2 s. It falls back to a full analysis when the catalog is missing, unreadable or built by another analyzer version, and can't be combined with `--limit`. |
| `classify` | Re-run **only the classification** on the existing catalog, without reading any audio. This is the fast loop for tuning the taxonomy, lexicon or overrides. |
| `report` | Print a data-quality report: coverage, audio-versus-name agreement per family, tempo and key agreement, facet distributions, and the list of samples flagged for review. |
| `verify` | Check that the catalog matches the files on disk (missing, extra, changed). |

To fix one file's classification by hand, add it to [`tools/SampleAnalyzer/overrides.json`](tools/SampleAnalyzer/overrides.json)
and run `classify`. A test (`CatalogConformanceTests`) fails if the committed catalog and the folder disagree.

Things worth knowing about the data:

* File names are strong evidence but not ground truth. Where the audio clearly contradicts a name for a well-defined family
  (kick, snare, hi-hat, tom, cymbal, clap, rim), the sample is flagged `NeedsReview` rather than silently trusted.
* The name is authoritative for tempo. Detected tempo often lands on a related pulse of a drum loop (dotted or triplet feel), so
  `RhythmFacet.TempoAgreesWithName` means "metrically related", not "equal".
* Loops are cut to whole bars, so `RhythmAnalyzer.SnapToLoopGrid` uses the clip's length to correct a confident detection: it
  moves an octave or triplet-feel mistake, and a slightly-off estimate, onto `60 * beats / duration` for 4, 8, 16, 32 or 64 beats
  (`report` prints the effect: 72 % of the files that name a tempo now match it to within 2 BPM, up from 43 %). Whether a loop is
  felt at 80 or at 160 is a naming convention the audio cannot settle, so when both fit, the one nearer 110 BPM wins; the
  remaining misses are almost all that choice. Whether a clip is a loop (`IsLoopLike`) is judged on the tempo *before* this
  correction, so the correction cannot vouch for itself.
* A detected tempo is only believed at a periodicity confidence of 0.30 or more (`RhythmFacet.TrustedBpm`): below that it matched
  the name for only 3 of 14 files. So `Sample.EffectiveTempoBpm` (the tempo filter, sort and badge) is the name's tempo, else a
  confident detection, else none: 127 of the 2,220 sounds have a tempo, 112 of them from the name.
* Octave numbers in names (`Bass Sub C#0`) use a different convention from the detected pitch (measured octave is usually one
  higher), so only the pitch class is compared.
* The bundled samples are mostly 24-bit (or `WAVE_FORMAT_EXTENSIBLE`). Android plays them as they are, so auditioning uses the
  original. The app's own `WavFile` (Editor, Trim, Mixer, Multi-Track) reads 16-bit PCM only, so **Add to library** converts a
  sample to 16-bit PCM (16-bit samples are copied unchanged); the ontology has its own reader and does not depend on `WavFile`.

## Testing

```bash
dotnet test --project OvertonesPlayground.Tests
```

The tests run on the desktop host: no device, emulator or MAUI workload run-time is needed, and the whole suite takes a few
seconds.

### What is tested where

The app targets `net10.0-android` only, so a host-run test project can't reference it. Instead
[`OvertonesPlayground.Tests`](OvertonesPlayground.Tests/OvertonesPlayground.Tests.csproj) compiles the app's own source files
straight into the test assembly, and only the ones that can run on a plain .NET host:

| Code | Tested how |
| --- | --- |
| `Models`, `Services/Interfaces`, `ViewModels` | Unit tests, with fakes for everything a view model depends on. |
| `OvertonesPlayground.Ontology` (a real `net10.0` library, referenced directly) | Unit tests on synthetic signals with known answers (pitch, loudness, stereo, tempo, clipping), real file names for the classifiers, and a conformance test of the committed catalog against the audio folder. |
| Service implementations that reach the device only through an injected interface (`IFileSystem`, `IPreferences`, `IFilePicker`, `IAudioManager`, `IAudioRecorder`...) | Unit tests, with real temp files and fakes for the platform API. The DSP code (filters, FFT, noise reduction, drum synthesis, WAV reading and writing) is plain C# and is tested directly. |
| `Platforms/**`, `Views/**`, `Controls/**`, `Themes/**`, `PermissionsService`, `ShellNavigationService`, every `*.xaml` | **Not** unit-tested: they need handlers, native controls, permissions or a running Shell. Check them on a device, for example with [DevFlow](https://github.com/dotnet/maui-labs) (`maui devflow ui tree`, `ui tap --automationId ...`). |

### Keeping code testable

View models and services must not call the static MAUI platform APIs (`Preferences.Default`, `FileSystem.AppDataDirectory`,
`Shell.Current`, `Clipboard.Default`, `DeviceDisplay.Current`...). They take the interface instead, and `MauiProgram`
registers the real implementation:

| Instead of | Inject |
| --- | --- |
| `Preferences.Default` | `IPreferences` |
| `FileSystem.AppDataDirectory` / `CacheDirectory` | `IFileSystem` |
| `FilePicker.Default` | `IFilePicker` |
| `Clipboard.Default`, `Share.Default` | `IClipboard`, `IShare` |
| `DeviceDisplay.Current`, `AppInfo.Current` | `IDeviceDisplay`, `IAppInfo` |
| `Shell.Current.GoToAsync(route)` | `INavigationService` |
| `Application.Current.UserAppTheme` | `IThemeService` |
| `TeachingTips` (static) | `ITeachingTipsService` |

The tests use `TestSupport/FakePreferences` and `TestSupport/TempFileSystem` for the first two, and NSubstitute for the rest.

### Adding a test for a new file

* A new view model, model or service interface is picked up automatically (they are linked by wildcard).
* A new **service implementation** has to be added to the explicit list in the test project's `.csproj`. That is deliberate, so a
  device-bound service is never linked by accident. If it won't compile there, it still calls a static platform API.
