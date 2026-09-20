using System.Text;
using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Tests.Services;

public sealed class SampleCatalogServiceTests : IDisposable
{
    private readonly TempFileSystem _fileSystem = new();

    public void Dispose() => _fileSystem.Dispose();

    private SampleCatalogService Create() => new(_fileSystem);

    private void AddCatalog(int samples = 3)
    {
        SampleCatalog catalog = SampleCatalog.Create(TestSamples.Library().Take(samples));
        using MemoryStream stream = new();
        SampleCatalogSerializer.Serialize(catalog, stream);
        _fileSystem.AddPackageFile(SampleCatalogService.CatalogAssetName, stream.ToArray());
    }

    [Fact]
    public async Task GetIndexAsync_ReadsTheCatalogAssetAndBuildsAnIndex()
    {
        AddCatalog(5);
        SampleCatalogService service = Create();

        SampleIndex index = await service.GetIndexAsync(TestContext.Current.CancellationToken);

        Assert.Equal(5, index.Count);
        Assert.NotNull(index.Find("Kick Test 1.wav"));
        Assert.True(service.IsLoaded);
        Assert.True(service.LoadDuration > TimeSpan.Zero);
    }

    [Fact]
    public async Task GetIndexAsync_BeforeTheFirstCall_ReportsNotLoaded()
    {
        AddCatalog();

        SampleCatalogService service = Create();

        Assert.False(service.IsLoaded);
        Assert.Equal(TimeSpan.Zero, service.LoadDuration);
        _ = await service.GetIndexAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetIndexAsync_CalledAgain_ReturnsTheSameIndexWithoutReadingTheAssetAgain()
    {
        AddCatalog();
        SampleCatalogService service = Create();

        SampleIndex first = await service.GetIndexAsync(TestContext.Current.CancellationToken);
        SampleIndex second = await service.GetIndexAsync(TestContext.Current.CancellationToken);

        Assert.Same(first, second);
        Assert.Equal(1, _fileSystem.PackageOpenCount);
    }

    [Fact]
    public async Task GetIndexAsync_CalledFromSeveralPlacesAtOnce_LoadsOnce()
    {
        AddCatalog();
        SampleCatalogService service = Create();

        SampleIndex[] indexes = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => service.GetIndexAsync(TestContext.Current.CancellationToken)));

        Assert.All(indexes, index => Assert.Same(indexes[0], index));
        Assert.Equal(1, _fileSystem.PackageOpenCount);
    }

    [Fact]
    public async Task GetIndexAsync_CatalogAssetMissing_ThrowsInvalidDataWithAHint()
    {
        SampleCatalogService service = Create();

        InvalidDataException ex = await Assert.ThrowsAsync<InvalidDataException>(() => service.GetIndexAsync(TestContext.Current.CancellationToken));

        Assert.Contains("sample-catalog.json", ex.Message, StringComparison.Ordinal);
        Assert.Contains("SampleAnalyzer", ex.Message, StringComparison.Ordinal);
        Assert.False(service.IsLoaded);
    }

    [Fact]
    public async Task GetIndexAsync_CatalogFromAnotherSchema_ThrowsInvalidData()
    {
        _fileSystem.AddPackageFile(SampleCatalogService.CatalogAssetName, Encoding.UTF8.GetBytes("{\"schemaVersion\":99,\"analyzerVersion\":\"x\",\"samples\":[]}"));
        SampleCatalogService service = Create();

        _ = await Assert.ThrowsAsync<InvalidDataException>(() => service.GetIndexAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetIndexAsync_AfterAFailure_CanBeRetriedOnceTheAssetExists()
    {
        SampleCatalogService service = Create();
        _ = await Assert.ThrowsAsync<InvalidDataException>(() => service.GetIndexAsync(TestContext.Current.CancellationToken));

        AddCatalog(4);
        SampleIndex index = await service.GetIndexAsync(TestContext.Current.CancellationToken);

        Assert.Equal(4, index.Count);
    }

    [Fact]
    public async Task GetIndexAsync_AlreadyCancelled_Throws()
    {
        AddCatalog();
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create().GetIndexAsync(cancelled.Token));
    }
}
