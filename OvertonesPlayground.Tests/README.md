# OvertonesPlayground.Tests

## Current state

This project is the host-run test suite for the repo. It targets `net10.0`, links the app's host-safe source files into the test assembly, and verifies the shared ontology, service logic, and view-model behaviors without needing a device or MAUI runtime. The Android-only app itself is intentionally not directly referenced from the test project.

Purpose
-------

This project holds the unit and integration tests for the OvertonesPlayground solution. Tests exercise:

- The Ontology (audio decoding, feature extraction, classification, indexing and cataloging).
- Service implementations (audio I/O, FFT, synthesis, file writers, playback and sample catalog services).
- ViewModels and UI-facing logic (thin view-model unit tests that avoid platform UI).
- Small functional checks of model classes and helpers.

Test framework and configuration
-------------------------------

- Tests use xUnit. The runner configuration is present in `xunit.runner.json` (default schema is included).
- Target framework: .NET 10 (matches the solution).

How to run
----------

- From the solution root (runs all test projects):

  dotnet test

- Run only this project:

  dotnet test OvertonesPlayground.Tests\OvertonesPlayground.Tests.csproj

- Run a single test or method by fully qualified name:

  dotnet test --filter "FullyQualifiedName=OvertonesPlayground.Tests.Ontology.RiffWavReaderTests.Read_AcidChunkWithTempo_ExposesTempoRootAndOneShotFlag"

- Visual Studio: use Test Explorer to run, debug or filter tests interactively.

Test organization
-----------------

- Folders mirror high-level functionality:
  - Ontology: tests for analysis, classification and indexing (RiffWavReaderTests, SpectralAnalyzerTests, CorpusClassifierTests, etc.).
  - Services: tests for audio services, FFT, synthesis and file I/O.
  - ViewModels: unit tests for view-models (LibraryViewModelTests, MixerViewModelTests, etc.).
  - TestSupport: small builders and helpers used by the tests (RiffBuilder, TestSignals, TempFileSystem, RealCatalog, TestSamples, WavTestFiles).

Test support utilities
----------------------

- RiffBuilder builds synthetic RIFF/WAVE bytes (including malformed shapes) so audio decoding code can be tested precisely.
- TestSignals generates PCM test signals and computes RMS values for assertions.
- TempFileSystem provides disposable temporary directories/files for I/O tests.
- RealCatalog loads the committed `OvertonesPlayground/Resources/Raw/sample-catalog.json` and builds a `SampleIndex`; tests that use RealCatalog validate behaviour against the real library rather than synthetic samples.

Data dependencies and long-running tests
---------------------------------------

- Some tests read `OvertonesPlayground/Resources/Raw/sample-catalog.json` (RealCatalog). If that file is not present, the tests that depend on it will fail. To generate a catalog use the `tools/SampleAnalyzer` tool (see tools/SampleAnalyzer/README.md) or ensure the repository includes the precomputed catalog.
- Most tests are synthetic and fast; tests that exercise the full real catalog may be slower and are grouped under Ontology tests that require the real data.

Conventions and guidance for adding tests
----------------------------------------

- Use xUnit `[Fact]` for single-case tests and `[Theory]` for parameterized cases when appropriate.
- Keep tests small and deterministic. Prefer synthetic inputs (RiffBuilder, TestSignals) rather than recording external audio when possible.
- When a test needs file I/O, use TempFileSystem to avoid leaving artifacts and to make tests parallel-safe.
- If a test must use the real catalog, make the dependency explicit in its name or comment so maintainers know it relies on repository data.

CI and parallelism
------------------

- The project is intended to run in CI via `dotnet test`.
- xUnit runner configuration lives in `xunit.runner.json`; adjust it if you need to change parallelization or diagnostic settings for CI.

Troubleshooting
---------------

- If many file-based tests fail with FileNotFoundException referring to `sample-catalog.json`, either generate the catalog with the SampleAnalyzer tool or exclude/skip tests that require real data when running light test passes.
- Use the Test Explorer in Visual Studio to run failing tests in debug mode for step-through diagnostics.
