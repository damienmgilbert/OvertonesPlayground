using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace SampleAnalyzer;

///<summary>
///Implements <c>analyze</c> (audio to catalog, optionally only for files that changed) and <c>classify</c> (catalog to
///catalog, audio untouched).
///</summary>
internal static class AnalyzeCommand
{
    #region Fields
    private static readonly string[] _audioExtensions = [".wav", ".aif", ".aiff", ".aifc"];
    #endregion

    #region Private methods
    private static IReadOnlyList<Sample> AnalyzeFiles(IReadOnlyList<string> files, string rawDirectory, int parallel)
    {
        if (files.Count == 0)
        {
            Console.WriteLine("No audio to analyse.");
            return [];
        }

        Console.WriteLine($"Analysing {files.Count} files from {rawDirectory} on {parallel} threads...");
        SampleAnalysisPipeline pipeline = new();
        ConcurrentBag<Sample> analyzed = [];
        int done = 0;
        long bytes = 0;
        Stopwatch clock = Stopwatch.StartNew();
        _ = Parallel.ForEach(
            files,
            new ParallelOptions { MaxDegreeOfParallelism = parallel },
            path =>
            {
                Sample sample = pipeline.AnalyzeFile(path);
                analyzed.Add(sample);
                long total = Interlocked.Add(ref bytes, sample.Asset.SizeBytes);
                int count = Interlocked.Increment(ref done);
                if (count % 100 == 0)
                {
                    Console.WriteLine($"  {count}/{files.Count} files, {total / 1_000_000.0:0} MB, {clock.Elapsed.TotalSeconds:0} s");
                }
            });

        Console.WriteLine($"Analysis took {clock.Elapsed.TotalSeconds:0.0} s.");
        return [.. analyzed];
    }

    private static int Classify(IReadOnlyList<Sample> analyzed, string outputPath, string overridesPath)
    {
        ClassificationOverrides overrides = ClassificationOverrides.Load(overridesPath);
        Console.WriteLine($"Classifying with {overrides.Count} manual override(s) from {overridesPath}.");
        IReadOnlyList<Sample> classified = new CorpusClassifier(overrides).Classify(analyzed);
        SampleCatalog catalog = SampleCatalog.Create(classified);
        SampleCatalogSerializer.Save(catalog, outputPath);
        Console.WriteLine($"Wrote {catalog.Samples.Count} samples to {outputPath} ({new FileInfo(outputPath).Length / 1_000_000.0:0.00} MB).");
        ReportCommand.PrintSummary(catalog);
        return 0;
    }

    private static string HashOf(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static bool IsOn(Dictionary<string, string> options, string name) => options.TryGetValue(name, out string? value) && !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);

    ///<summary>
    ///Reads the existing catalog (a missing or unreadable one just means everything is analysed) and compares it with
    ///the folder.
    ///</summary>
    private static CatalogUpdatePlan PlanUpdate(string catalogPath, string rawDirectory, IReadOnlyList<string> files)
    {
        SampleCatalog? existing = null;
        if (File.Exists(catalogPath))
        {
            try
            {
                using FileStream stream = File.OpenRead(catalogPath);
                existing = SampleCatalogSerializer.Deserialize(stream);
            }
            catch (Exception ex) when (ex is InvalidDataException or JsonException)
            {
                Console.WriteLine($"The existing catalog {catalogPath} can't be read ({ex.Message}).");
            }
        }

        Stopwatch clock = Stopwatch.StartNew();
        List<DiskFile> disk = [.. files.Select(path => new DiskFile(Path.GetFileName(path), new FileInfo(path).Length))];
        CatalogUpdatePlan plan = CatalogUpdatePlan.Create(existing, disk, name => HashOf(Path.Combine(rawDirectory, name)));
        Console.WriteLine($"Compared {disk.Count} files with the catalog in {clock.Elapsed.TotalSeconds:0.0} s.");
        return plan;
    }

    private static void PrintPlan(CatalogUpdatePlan plan)
    {
        if (plan.FullReanalysisReason is not null)
        {
            Console.WriteLine($"Nothing can be reused: {plan.FullReanalysisReason}.");
            return;
        }

        Console.WriteLine($"Keeping {plan.Reused.Count} unchanged samples; {plan.ToAnalyze.Count} new or changed; {plan.Removed.Count} removed.");
        foreach (string name in plan.ToAnalyze.Take(20))
        {
            Console.WriteLine($"  ~ {name}");
        }

        foreach (string name in plan.Removed.Take(20))
        {
            Console.WriteLine($"  - {name}");
        }
    }
    #endregion

    #region Internal methods
    internal static int Reclassify(string catalogPath, string outputPath, string overridesPath)
    {
        SampleCatalog catalog;
        using (FileStream stream = File.OpenRead(catalogPath))
        {
            catalog = SampleCatalogSerializer.Deserialize(stream);
        }

        Console.WriteLine($"Re-classifying {catalog.Samples.Count} samples from {catalogPath} (no audio is read).");
        return Classify(catalog.Samples, outputPath, overridesPath);
    }

    ///<summary>
    ///Analyses the audio in <paramref name="rawDirectory"/> and writes the catalog to <paramref name="outputPath"/>.
    ///With ///<c>--changed-only</c>, samples of the catalog at <paramref name="catalogPath"/> whose file is unchanged
    ///are kept and only new or changed files are decoded; the whole corpus is classified again either way, because the
    ///signal classifier learns from the other files and the overrides may have changed.
    ///</summary>
    internal static int Run(string rawDirectory, string catalogPath, string outputPath, string overridesPath, Dictionary<string, string> options)
    {
        if (!Directory.Exists(rawDirectory))
        {
            throw new ArgumentException($"The audio folder '{rawDirectory}' does not exist.");
        }

        bool changedOnly = IsOn(options, "changed-only");
        if (changedOnly && options.ContainsKey("limit"))
        {
            throw new ArgumentException("--limit writes a partial catalog, so it can't be combined with --changed-only.");
        }

        List<string> files = [.. Directory.EnumerateFiles(rawDirectory).Where(path => _audioExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)).Order(StringComparer.OrdinalIgnoreCase)];
        if (options.TryGetValue("limit", out string? limit))
        {
            files = [.. files.Take(int.Parse(limit, CultureInfo.InvariantCulture))];
        }

        int parallel = options.TryGetValue("parallel", out string? p) ? int.Parse(p, CultureInfo.InvariantCulture) : Environment.ProcessorCount;

        List<Sample> samples = [];
        List<string> toAnalyze = files;
        if (changedOnly)
        {
            CatalogUpdatePlan plan = PlanUpdate(catalogPath, rawDirectory, files);
            samples.AddRange(plan.Reused);
            Dictionary<string, string> pathsByName = files.ToDictionary(path => new FileInfo(path).Name, StringComparer.Ordinal);
            toAnalyze = [.. plan.ToAnalyze.Select(name => pathsByName[name])];
            PrintPlan(plan);
        }

        samples.AddRange(AnalyzeFiles(toAnalyze, rawDirectory, parallel));
        return Classify(samples, outputPath, overridesPath);
    }
    #endregion
}
