using System.Collections.Concurrent;
using System.Diagnostics;

namespace SampleAnalyzer;

///<summary>
///Implements <c>analyze</c> (audio to catalog) and <c>classify</c> (catalog to catalog, audio untouched).
///</summary>
internal static class AnalyzeCommand
{
    #region Constants
    private static readonly string[] _audioExtensions = [".wav", ".aif", ".aiff", ".aifc"];
    #endregion

    #region Public methods
    internal static int Run(string rawDirectory, string outputPath, string overridesPath, Dictionary<string, string> options)
    {
        if (!Directory.Exists(rawDirectory))
        {
            throw new ArgumentException($"The audio folder '{rawDirectory}' does not exist.");
        }

        List<string> files = [.. Directory.EnumerateFiles(rawDirectory)
            .Where(path => _audioExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)];
        if (options.TryGetValue("limit", out string? limit))
        {
            files = [.. files.Take(int.Parse(limit, System.Globalization.CultureInfo.InvariantCulture))];
        }

        int parallel = options.TryGetValue("parallel", out string? p) ? int.Parse(p, System.Globalization.CultureInfo.InvariantCulture) : Environment.ProcessorCount;
        Console.WriteLine($"Analysing {files.Count} files from {rawDirectory} on {parallel} threads...");

        SampleAnalysisPipeline pipeline = new();
        ConcurrentBag<Sample> analyzed = [];
        int done = 0;
        long bytes = 0;
        Stopwatch clock = Stopwatch.StartNew();
        _ = Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = parallel }, path =>
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
        return Classify([.. analyzed], outputPath, overridesPath);
    }

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
    #endregion

    #region Private methods
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
    #endregion
}
