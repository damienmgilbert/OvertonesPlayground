namespace OvertonesPlayground.Ontology.Catalog;

///<summary>
///What an incremental <c>analyze</c> has to do to bring an existing catalog in line with the audio folder: which samples
///can be kept as they are, which files have to be (re)analysed, and which catalog entries no longer have a file.
///</summary>
///<param name="Reused">Samples of the existing catalog whose file is unchanged, kept without touching the audio.</param>
///<param name="ToAnalyze">Files that are new or changed since they were analysed, or all files when nothing can be reused.</param>
///<param name="Removed">Catalog entries whose file is gone; they are dropped.</param>
///<param name="FullReanalysisReason">Why nothing was reused (no catalog, an unreadable one, or an older analyzer), or null when the catalog was used.</param>
public sealed record CatalogUpdatePlan(
    IReadOnlyList<Sample> Reused,
    IReadOnlyList<string> ToAnalyze,
    IReadOnlyList<string> Removed,
    string? FullReanalysisReason)
{
    ///<summary>
    ///Compares an existing catalog with the files on disk. A file is reused when the catalog has an entry of exactly the same
    ///name whose size and content hash both match; the hash is only computed for files whose size already matches. Nothing is
    ///reused when there is no catalog or when it was written by a different schema or analyzer version, because the numbers in
    ///it would not be what this build measures.
    ///</summary>
    ///<param name="existing">The catalog on disk, or null when there is none.</param>
    ///<param name="files">Every audio file in the folder.</param>
    ///<param name="computeHash">Lower-case hex SHA-256 of a file's content, by file name. Called in parallel, so it must be thread-safe.</param>
    public static CatalogUpdatePlan Create(SampleCatalog? existing, IReadOnlyList<DiskFile> files, Func<string, string> computeHash)
    {
        List<string> all = [.. files.Select(file => file.Name).Order(StringComparer.OrdinalIgnoreCase)];
        string? reason = FullReanalysisReasonFor(existing);
        if (existing is null || reason is not null)
        {
            return new CatalogUpdatePlan([], all, [], reason);
        }

        Dictionary<string, Sample> previous = existing.Samples.ToDictionary(sample => sample.Id, StringComparer.Ordinal);
        bool[] isUnchanged = new bool[files.Count];
        _ = Parallel.For(0, files.Count, index =>
        {
            DiskFile file = files[index];
            isUnchanged[index] = previous.TryGetValue(file.Name, out Sample? sample)
                && sample.Asset.SizeBytes == file.SizeBytes
                && string.Equals(sample.Asset.ContentHash, computeHash(file.Name), StringComparison.Ordinal);
        });

        List<Sample> reused = [];
        List<string> toAnalyze = [];
        for (int index = 0; index < files.Count; index++)
        {
            if (isUnchanged[index])
            {
                reused.Add(previous[files[index].Name]);
            }
            else
            {
                toAnalyze.Add(files[index].Name);
            }
        }

        HashSet<string> present = new(files.Select(file => file.Name), StringComparer.Ordinal);
        List<string> removed = [.. previous.Keys.Where(id => !present.Contains(id)).Order(StringComparer.OrdinalIgnoreCase)];
        toAnalyze.Sort(StringComparer.OrdinalIgnoreCase);
        return new CatalogUpdatePlan(reused, toAnalyze, removed, null);
    }

    private static string? FullReanalysisReasonFor(SampleCatalog? existing) => existing switch
    {
        null => "there is no usable catalog",
        { SchemaVersion: var schema } when schema != SampleCatalog.CurrentSchemaVersion =>
            $"the catalog has schema {schema} and this build writes schema {SampleCatalog.CurrentSchemaVersion}",
        { AnalyzerVersion: var analyzer } when analyzer != SampleCatalog.CurrentAnalyzerVersion =>
            $"the catalog was built by analyzer {analyzer} and the current one is {SampleCatalog.CurrentAnalyzerVersion}",
        _ => null,
    };
}
