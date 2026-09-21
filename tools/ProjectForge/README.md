# ProjectForge

Purpose
-------

ProjectForge proposes the ready-made Launchpad setups ("presets") that ship with the app. It runs the Launchpad project generator
(`OvertonesPlayground/Services/Implementations/LaunchpadGenerator.cs`) over every style in
`OvertonesPlayground.Ontology/Data/styles.json`. For each style it tries every kit the style lists, a spread of the style's tempos
and several seeds. It keeps only the setups that pass `LaunchpadQuality`, scores them by how much they give to play, and picks the
best few, each from a different kit.

Scoring
-------

The score adds up:

- pads filled
- loops, which count three times
- harmony pads, which count one and a half times
- two points for every distinct instrument
- two points for every drum column with six or more sounds

Usage
-----

    dotnet run --project tools/ProjectForge -- [--catalog <sample-catalog.json>] [--seeds <n>] [--per-style <n>] [--out <presets.json>]

- `--catalog`: the catalog to generate from. The default is the app's `Resources/Raw/sample-catalog.json`.
- `--seeds`: how many seeds to try for each kit (default 6).
- `--per-style`: how many presets to keep for each style (default 4).
- `--out`: where to write the JSON. Without it, the JSON goes to standard output. Progress and each pick go to standard error.

The output is a JSON object with one list of presets per style key, `{ "house": [ { "id", "name", "seed", "tempo", "key", "kit" } ] }`.
Copy each list into the style's `"presets"` in `styles.json`.

When to run it
--------------

Run it again whenever the sample catalog, the style profiles or the generator change. A preset stores its tempo, key, kit and seed,
not a list of files. If the sound bank changes, the same preset can therefore produce different sounds.
`LaunchpadGeneratorTests.Presets_EveryPresetBuildsAndPassesTheChecks` fails when a preset no longer passes the checks.
