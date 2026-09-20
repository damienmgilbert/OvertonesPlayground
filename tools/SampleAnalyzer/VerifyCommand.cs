using System.Security.Cryptography;

namespace SampleAnalyzer;

///<summary>
///Implements <c>verify</c>: does the catalog describe exactly the files on disk?
///</summary>
internal static class VerifyCommand
{
    #region Public methods
    internal static int Run(string catalogPath, string rawDirectory)
    {
        using FileStream stream = File.OpenRead(catalogPath);
        SampleCatalog catalog = SampleCatalogSerializer.Deserialize(stream);
        Dictionary<string, Sample> byId = catalog.Samples.ToDictionary(sample => sample.Id, StringComparer.OrdinalIgnoreCase);

        string[] audioExtensions = [".wav", ".aif", ".aiff", ".aifc"];
        List<string> files = [.. Directory.EnumerateFiles(rawDirectory).Where(path => audioExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))];

        List<string> missing = [.. files.Select(Path.GetFileName).OfType<string>().Where(name => !byId.ContainsKey(name))];
        HashSet<string> present = new(files.Select(Path.GetFileName).OfType<string>(), StringComparer.OrdinalIgnoreCase);
        List<string> extra = [.. byId.Keys.Where(id => !present.Contains(id))];

        List<string> stale = [];
        foreach (string path in files)
        {
            string name = Path.GetFileName(path);
            if (byId.TryGetValue(name, out Sample? sample))
            {
                string hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
                if (hash != sample.Asset.ContentHash)
                {
                    stale.Add(name);
                }
            }
        }

        Console.WriteLine($"Catalog: schema {catalog.SchemaVersion}, analyzer {catalog.AnalyzerVersion}, {catalog.Samples.Count} samples.");
        Console.WriteLine($"Folder:  {files.Count} audio files.");
        Console.WriteLine($"Files with no catalog entry: {missing.Count}");
        foreach (string name in missing.Take(20))
        {
            Console.WriteLine($"  + {name}");
        }

        Console.WriteLine($"Catalog entries with no file: {extra.Count}");
        foreach (string name in extra.Take(20))
        {
            Console.WriteLine($"  - {name}");
        }

        Console.WriteLine($"Entries whose file changed since analysis: {stale.Count}");
        foreach (string name in stale.Take(20))
        {
            Console.WriteLine($"  ~ {name}");
        }

        bool isStaleVersion = catalog.AnalyzerVersion != SampleCatalog.CurrentAnalyzerVersion;
        if (isStaleVersion)
        {
            Console.WriteLine($"Catalog was built by analyzer {catalog.AnalyzerVersion}; current is {SampleCatalog.CurrentAnalyzerVersion}.");
        }

        bool isClean = missing.Count == 0 && extra.Count == 0 && stale.Count == 0 && !isStaleVersion;
        Console.WriteLine(isClean ? "OK: the catalog matches the folder." : "MISMATCH: re-run `analyze`.");
        return isClean ? 0 : 1;
    }
    #endregion
}
