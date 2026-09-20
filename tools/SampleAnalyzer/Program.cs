using System.Diagnostics;

namespace SampleAnalyzer;

///<summary>
///Command-line entry point. Commands:
///<code>
///analyze   decode every audio file (or, with --changed-only, only new or changed ones), measure it, classify the corpus and write the catalog
///classify  re-run only the classification on an existing catalog (no audio is read; use it to tune the lexicon)
///report    print a data-quality report for a catalog
///verify    check that a catalog matches the files on disk
///</code>
///</summary>
internal static class Program
{
    #region Private methods
    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            if (directory.GetFiles("OvertonesPlayground.slnx").Length > 0)
            {
                return directory.FullName;
            }
        }

        return Directory.GetCurrentDirectory();
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        Dictionary<string, string> options = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < args.Length; i++)
        {
            string arg = args[i];
            bool isOption = arg.StartsWith("--", StringComparison.Ordinal);
            if (!isOption)
            {
                throw new ArgumentException($"Unexpected argument '{arg}'.");
            }

            string key = arg[2..];
            bool hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
            options[key] = hasValue ? args[++i] : "true";
        }

        return options;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            SampleAnalyzer - builds the sample ontology catalog for OvertonesPlayground.

              analyze  [--raw <dir>] [--out <catalog.json>] [--overrides <overrides.json>] [--parallel <n>] [--limit <n>]
                       [--changed-only [--catalog <catalog.json>]]
              classify [--catalog <catalog.json>] [--out <catalog.json>] [--overrides <overrides.json>]
              report   [--catalog <catalog.json>] [--review <n>]
              verify   [--catalog <catalog.json>] [--raw <dir>]

            --changed-only keeps the samples of the existing catalog whose file is unchanged (same size and SHA-256) and decodes
            only new or changed files. Everything is classified again, so overrides and taxonomy edits still apply. It analyses
            everything when the catalog is missing, unreadable, or was written by another analyzer version.

            Defaults resolve from the repository root: audio in OvertonesPlayground/Resources/Raw, catalog in
            OvertonesPlayground/Resources/Raw/sample-catalog.json, overrides in tools/SampleAnalyzer/overrides.json.
            """);
    }
    #endregion

    #region Public methods
    internal static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        try
        {
            Dictionary<string, string> options = ParseOptions(args);
            string root = FindRepositoryRoot();
            string raw = options.GetValueOrDefault("raw") ?? Path.Combine(root, "OvertonesPlayground", "Resources", "Raw");
            string catalogPath = options.GetValueOrDefault("catalog") ?? options.GetValueOrDefault("out") ?? Path.Combine(raw, "sample-catalog.json");
            string overridesPath = options.GetValueOrDefault("overrides") ?? Path.Combine(root, "tools", "SampleAnalyzer", "overrides.json");
            Stopwatch clock = Stopwatch.StartNew();

            int result = args[0].ToLowerInvariant() switch
            {
                "analyze" => AnalyzeCommand.Run(raw, catalogPath, options.GetValueOrDefault("out") ?? catalogPath, overridesPath, options),
                "classify" => AnalyzeCommand.Reclassify(options.GetValueOrDefault("catalog") ?? catalogPath, options.GetValueOrDefault("out") ?? catalogPath, overridesPath),
                "report" => ReportCommand.Run(catalogPath, options),
                "verify" => VerifyCommand.Run(catalogPath, raw),
                _ => throw new ArgumentException($"Unknown command '{args[0]}'."),
            };

            Console.WriteLine($"Done in {clock.Elapsed.TotalSeconds:0.0} s.");
            return result;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 2;
        }
    }
    #endregion
}
