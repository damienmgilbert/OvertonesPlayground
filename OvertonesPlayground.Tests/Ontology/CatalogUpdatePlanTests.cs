namespace OvertonesPlayground.Tests.Ontology;

public sealed class CatalogUpdatePlanTests
{
    #region Private methods
    // TestSamples.Make gives every sample a size of 1000 bytes and the hash "hash-<name>".
    private static SampleCatalog Catalog(params string[] names) => new(SampleCatalog.CurrentSchemaVersion, SampleCatalog.CurrentAnalyzerVersion, [.. names.Select(name => TestSamples.Make(name))]);

    private static string HashOf(string fileName) => $"hash-{Path.GetFileNameWithoutExtension(fileName)}";

    private static DiskFile OnDisk(string name, long size = 1000) => new($"{name}.wav", size);

    private static CatalogUpdatePlan Plan(SampleCatalog? existing, IReadOnlyList<DiskFile> files, Func<string, string>? hash = null) => CatalogUpdatePlan.Create(existing, files, hash ?? HashOf);
    #endregion

    #region Public methods
    [Fact]
    public void Create_ANameThatDiffersOnlyInCase_IsAnotherAssetBecauseAndroidAssetNamesAreCaseSensitive()
    {
        CatalogUpdatePlan plan = Plan(Catalog("kick 1"), [OnDisk("Kick 1")]);

        Assert.Empty(plan.Reused);
        Assert.Equal(["Kick 1.wav"], plan.ToAnalyze);
        Assert.Equal(["kick 1.wav"], plan.Removed);
    }

    [Fact]
    public void Create_CatalogEntryWithNoFile_IsRemoved()
    {
        CatalogUpdatePlan plan = Plan(Catalog("A", "Gone", "Also gone"), [OnDisk("A")]);

        Assert.Equal(["A"], plan.Reused.Select(s => s.Name));
        Assert.Equal(["Also gone.wav", "Gone.wav"], plan.Removed);
        Assert.Empty(plan.ToAnalyze);
    }

    [Fact]
    public void Create_CatalogFromAnotherAnalyzerVersion_AnalysesEverythingWithoutHashing()
    {
        SampleCatalog old = new(SampleCatalog.CurrentSchemaVersion, "0.1.0", [TestSamples.Make("A")]);
        int hashes = 0;

        CatalogUpdatePlan plan = Plan(
                                 old,
                                 [OnDisk("A")],
                                 name =>
                                 {
                                     _ = Interlocked.Increment(ref hashes);
                                     return HashOf(name);
                                 });

        Assert.Empty(plan.Reused);
        Assert.Equal(["A.wav"], plan.ToAnalyze);
        Assert.Equal(0, hashes);
        Assert.Contains("analyzer 0.1.0", plan.FullReanalysisReason, StringComparison.Ordinal);
        Assert.Contains(SampleCatalog.CurrentAnalyzerVersion, plan.FullReanalysisReason, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_CatalogWithAnotherSchema_AnalysesEverything()
    {
        SampleCatalog old = new(SampleCatalog.CurrentSchemaVersion + 1, SampleCatalog.CurrentAnalyzerVersion, [TestSamples.Make("A")]);

        CatalogUpdatePlan plan = Plan(old, [OnDisk("A")]);

        Assert.Empty(plan.Reused);
        Assert.Equal(["A.wav"], plan.ToAnalyze);
        Assert.Contains("schema", plan.FullReanalysisReason, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_EmptyFolder_RemovesEveryEntry()
    {
        CatalogUpdatePlan plan = Plan(Catalog("A", "B"), []);

        Assert.Empty(plan.Reused);
        Assert.Empty(plan.ToAnalyze);
        Assert.Equal(["A.wav", "B.wav"], plan.Removed);
    }

    [Fact]
    public void Create_EveryFileUnchanged_ReusesEverySampleAndAnalysesNothing()
    {
        CatalogUpdatePlan plan = Plan(Catalog("A", "B", "C"), [OnDisk("A"), OnDisk("B"), OnDisk("C")]);

        Assert.Equal(["A", "B", "C"], plan.Reused.Select(s => s.Name));
        Assert.Empty(plan.ToAnalyze);
        Assert.Empty(plan.Removed);
        Assert.Null(plan.FullReanalysisReason);
    }

    [Fact]
    public void Create_FileWithADifferentSize_IsAnalysedWithoutBeingHashed()
    {
        List<string> hashed = [];

        CatalogUpdatePlan plan = Plan(
                                 Catalog("A", "B"),
                                 [OnDisk("A", 2048), OnDisk("B")],
                                 name =>
                                 {
                                     lock (hashed)
                                     {
                                         hashed.Add(name);
                                     }

                                     return HashOf(name);
                                 });

        Assert.Equal(["A.wav"], plan.ToAnalyze);
        Assert.Equal(["B.wav"], hashed);
    }

    [Fact]
    public void Create_FileWithTheSameSizeButOtherContent_IsAnalysed()
    {
        CatalogUpdatePlan plan = Plan(Catalog("A", "B"), [OnDisk("A"), OnDisk("B")], name => name == "B.wav" ? "different" : HashOf(name));

        Assert.Equal(["A"], plan.Reused.Select(s => s.Name));
        Assert.Equal(["B.wav"], plan.ToAnalyze);
    }

    [Fact]
    public void Create_NewFile_IsAnalysed()
    {
        CatalogUpdatePlan plan = Plan(Catalog("A"), [OnDisk("A"), OnDisk("New")]);

        Assert.Equal(["A"], plan.Reused.Select(s => s.Name));
        Assert.Equal(["New.wav"], plan.ToAnalyze);
        Assert.Empty(plan.Removed);
    }

    [Fact]
    public void Create_NoCatalog_AnalysesEverything()
    {
        CatalogUpdatePlan plan = Plan(null, [OnDisk("B"), OnDisk("a")]);

        Assert.Empty(plan.Reused);
        Assert.Equal(["a.wav", "B.wav"], plan.ToAnalyze);
        Assert.Equal("there is no usable catalog", plan.FullReanalysisReason);
    }

    [Fact]
    public void Create_ReusesTheVeryInstanceFromTheCatalog()
    {
        SampleCatalog catalog = Catalog("A");

        CatalogUpdatePlan plan = Plan(catalog, [OnDisk("A")]);

        Assert.Same(catalog.Samples[0], plan.Reused.Single());
    }
    #endregion
}
