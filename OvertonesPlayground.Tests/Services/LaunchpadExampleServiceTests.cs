using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Tests.Services;

public sealed class LaunchpadExampleServiceTests : IDisposable
{
    private readonly ISampleAssetStore _assets = Substitute.For<ISampleAssetStore>();
    private readonly ISampleCatalogService _catalog = Substitute.For<ISampleCatalogService>();
    private readonly TempFileSystem _files = new();

    public LaunchpadExampleServiceTests()
    {
        _catalog.GetIndexAsync(Arg.Any<CancellationToken>()).Returns(RealCatalog.Index);
        _assets.CopyToAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            string directory = call.ArgAt<string>(1);
            _ = Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, call.ArgAt<string>(0));
            File.WriteAllBytes(path, [1, 2, 3]);
            return Task.FromResult(path);
        });
    }

    public void Dispose() => _files.Dispose();

    private LaunchpadExampleService Create() => new(_catalog, _assets, _files);

    private static IEnumerable<string> PathsUsedBy(LaunchpadProject project) =>
        project.Pads.Select(pad => pad.ClipPath).Concat(project.Sequence.Tracks.Select(track => track.ClipPath)).OfType<string>();

    [Fact]
    public void Examples_AreTheSetupsTheRecipesKnow()
    {
        Assert.Same(LaunchpadExamples.All, Create().Examples);
    }

    [Theory]
    [MemberData(nameof(LaunchpadExamplesTests.ExampleIds), MemberType = typeof(LaunchpadExamplesTests))]
    public async Task CreateAsync_CopiesEverySoundOnceAndPointsPadsAndTracksAtTheCopies(string id)
    {
        LaunchpadProject project = await Create().CreateAsync(id, TestContext.Current.CancellationToken);

        string directory = _files.InAppData(LaunchpadExampleService.SamplesFolderName);
        List<string> paths = [.. PathsUsedBy(project).Distinct()];
        Assert.NotEmpty(paths);
        Assert.All(paths, path =>
        {
            Assert.True(File.Exists(path), path);
            Assert.Equal(directory, Path.GetDirectoryName(path));
        });
        _ = await _assets.Received(paths.Count).CopyToAsync(Arg.Any<string>(), directory, Arg.Any<CancellationToken>());
        Assert.All(project.Pads, pad => Assert.StartsWith(pad.Label, Path.GetFileNameWithoutExtension(pad.ClipPath), StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateAsync_AskedAgain_ReusesTheCopiesInsteadOfCopyingThemAgain()
    {
        LaunchpadExampleService service = Create();
        LaunchpadProject first = await service.CreateAsync(LaunchpadExamples.BoomBapId, TestContext.Current.CancellationToken);
        _assets.ClearReceivedCalls();

        LaunchpadProject second = await service.CreateAsync(LaunchpadExamples.BoomBapId, TestContext.Current.CancellationToken);

        _ = await _assets.DidNotReceive().CopyToAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.Equal(PathsUsedBy(first), PathsUsedBy(second));
    }

    [Fact]
    public async Task CreateAsync_ASoundAlreadyOnDisk_IsNotCopiedAgain()
    {
        string directory = _files.InAppData(LaunchpadExampleService.SamplesFolderName);
        _ = Directory.CreateDirectory(directory);
        string kept = Path.Combine(directory, "Break Ghosts 90 bpm.wav");
        await File.WriteAllBytesAsync(kept, [9, 9, 9], TestContext.Current.CancellationToken);

        LaunchpadProject project = await Create().CreateAsync(LaunchpadExamples.BoomBapId, TestContext.Current.CancellationToken);

        _ = await _assets.DidNotReceive().CopyToAsync("Break Ghosts 90 bpm.wav", Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.Contains(project.Pads, pad => pad.ClipPath == kept);
        Assert.Equal([9, 9, 9], await File.ReadAllBytesAsync(kept, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_TheSamplesLiveOutsideTheLibrariesFolder()
    {
        LaunchpadProject project = await Create().CreateAsync(LaunchpadExamples.ClubId, TestContext.Current.CancellationToken);

        Assert.All(PathsUsedBy(project), path => Assert.DoesNotContain(Path.DirectorySeparatorChar + "Clips" + Path.DirectorySeparatorChar, path, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_UnknownOrBlankId_ThrowsBeforeTouchingTheBankOrTheDisk(string id)
    {
        _ = await Assert.ThrowsAnyAsync<ArgumentException>(() => Create().CreateAsync(id, TestContext.Current.CancellationToken));

        _ = await _catalog.DidNotReceive().GetIndexAsync(Arg.Any<CancellationToken>());
        _ = await _assets.DidNotReceive().CopyToAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_Cancelled_StopsCopying()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create().CreateAsync(LaunchpadExamples.TrapId, cts.Token));

        _ = await _assets.DidNotReceive().CopyToAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_TheCatalogCannotBeRead_PassesTheFailureOn()
    {
        _catalog.GetIndexAsync(Arg.Any<CancellationToken>()).Returns<Task<SampleIndex>>(_ => throw new InvalidDataException("no catalog"));

        _ = await Assert.ThrowsAsync<InvalidDataException>(() => Create().CreateAsync(LaunchpadExamples.BoomBapId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_ASoundIsMissingFromThePackage_PassesTheFailureOn()
    {
        _assets.CopyToAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns<Task<string>>(_ => throw new FileNotFoundException("gone"));

        _ = await Assert.ThrowsAsync<FileNotFoundException>(() => Create().CreateAsync(LaunchpadExamples.BoomBapId, TestContext.Current.CancellationToken));
    }
}
