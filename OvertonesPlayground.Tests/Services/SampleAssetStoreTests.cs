using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Tests.Services;

public sealed class SampleAssetStoreTests : IDisposable
{
    #region Fields
    private readonly TempFileSystem _fileSystem = new();
    #endregion

    #region Private methods
    private byte[] AddAsset(string name, int size = 64)
    {
        byte[] bytes = RiffBuilder.Wav16(1, 44100, [.. Enumerable.Range(0, size).Select(i => (short)((i * 7) + name.Length))]);
        _fileSystem.AddPackageFile(name, bytes);
        return bytes;
    }

    private static void AssertNear(int expected, short actual) => Assert.True(Math.Abs(expected - actual) <= 1, $"expected {expected} (+/- 1) but was {actual}");

    private SampleAssetStore Create() => new(_fileSystem);
    #endregion

    #region Public methods
    [Fact]
    public async Task CopyToAsync_24BitFile_BecomesA16BitFileTheEditorsCanRead()
    {
        int half = 1 << 22;
        _fileSystem.AddPackageFile("Deep.wav", RiffBuilder.Wav24(2, 48000, half, -half, 8388607, -8388608, 0, 0));

        string path = await Create().CopyToAsync("Deep.wav", _fileSystem.InAppData("Clips"), TestContext.Current.CancellationToken);

        WavFile converted = await WavFile.ReadAsync(path);
        Assert.Equal(16, converted.BitsPerSample);
        Assert.Equal(2, converted.Channels);
        Assert.Equal(48000, converted.SampleRate);
        Assert.Equal(6, converted.Samples.Length);
        AssertNear(16384, converted.Samples[0]);
        AssertNear(-16384, converted.Samples[1]);
        Assert.Equal(short.MaxValue, converted.Samples[2]);
        AssertNear(-32767, converted.Samples[3]);
        Assert.Equal(0, converted.Samples[4]);
    }

    [Fact]
    public async Task CopyToAsync_AFileThatIsNotAWav_IsCopiedAsItIs()
    {
        byte[] notWav = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        _fileSystem.AddPackageFile("Odd.aif", notWav);

        string path = await Create().CopyToAsync("Odd.aif", _fileSystem.InAppData("Clips"), TestContext.Current.CancellationToken);

        Assert.Equal(notWav, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CopyToAsync_ExtensibleAndFloatFiles_AreConvertedToo()
    {
        _fileSystem.AddPackageFile("Ext.wav", new RiffBuilder().FormatExtensible(1, 1, 96000, 24).Data(RiffBuilder.Pcm24(1 << 21, -(1 << 21))).Build());
        _fileSystem.AddPackageFile("Float.wav", new RiffBuilder().Format(3, 1, 44100, 32).Data(RiffBuilder.Float32(0.5f, -0.25f, 2.0f)).Build());
        SampleAssetStore store = Create();

        WavFile extensible = await WavFile.ReadAsync(await store.CopyToAsync("Ext.wav", _fileSystem.InAppData("Clips"), TestContext.Current.CancellationToken));
        WavFile floating = await WavFile.ReadAsync(await store.CopyToAsync("Float.wav", _fileSystem.InAppData("Clips"), TestContext.Current.CancellationToken));

        Assert.Equal(96000, extensible.SampleRate);
        AssertNear(8192, extensible.Samples[0]);
        AssertNear(-8192, extensible.Samples[1]);
        AssertNear(16384, floating.Samples[0]);
        AssertNear(-8192, floating.Samples[1]);
        Assert.Equal(short.MaxValue, floating.Samples[2]);
    }

    [Fact]
    public async Task CopyToAsync_NameAlreadyThere_UsesANumberedNameAndKeepsTheOriginal()
    {
        _ = AddAsset("Clap Crunch.wav");
        SampleAssetStore store = Create();
        string destination = _fileSystem.InAppData("Clips");

        string first = await store.CopyToAsync("Clap Crunch.wav", destination, TestContext.Current.CancellationToken);
        string second = await store.CopyToAsync("Clap Crunch.wav", destination, TestContext.Current.CancellationToken);
        string third = await store.CopyToAsync("Clap Crunch.wav", destination, TestContext.Current.CancellationToken);

        Assert.Equal("Clap Crunch.wav", Path.GetFileName(first));
        Assert.Equal("Clap Crunch (2).wav", Path.GetFileName(second));
        Assert.Equal("Clap Crunch (3).wav", Path.GetFileName(third));
        Assert.Equal(3, Directory.GetFiles(destination).Length);
    }

    [Fact]
    public async Task CopyToAsync_Plain16BitFile_IsCopiedByteForByte()
    {
        byte[] file = RiffBuilder.Wav16(1, 44100, 0, 1000, -1000, short.MaxValue, short.MinValue);
        _fileSystem.AddPackageFile("Sixteen.wav", file);

        string path = await Create().CopyToAsync("Sixteen.wav", _fileSystem.InAppData("Clips"), TestContext.Current.CancellationToken);

        Assert.Equal(file, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CopyToAsync_TheCopyIsNotAffectedWhenTheCacheEvictsTheOriginal()
    {
        byte[] bytes = AddAsset("Keeper.wav");
        SampleAssetStore store = Create();
        string permanent = await store.CopyToAsync("Keeper.wav", _fileSystem.InAppData("Clips"), TestContext.Current.CancellationToken);

        File.Delete(Path.Combine(_fileSystem.CacheDirectory, "sample-bank", "Keeper.wav"));

        Assert.Equal(bytes, await File.ReadAllBytesAsync(permanent, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CopyToAsync_WritesAPermanentCopyToTheDestination()
    {
        byte[] bytes = AddAsset("Clap Crunch.wav");
        string destination = _fileSystem.InAppData("Clips");

        string path = await Create().CopyToAsync("Clap Crunch.wav", destination, TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(destination, "Clap Crunch.wav"), path);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    public void Dispose() => _fileSystem.Dispose();

    [Fact]
    public async Task GetLocalPathAsync_AskedTwice_ReturnsTheSameFileWithoutCopyingAgain()
    {
        _ = AddAsset("Snare Test.wav");
        SampleAssetStore store = Create();

        string first = await store.GetLocalPathAsync("Snare Test.wav", TestContext.Current.CancellationToken);
        string second = await store.GetLocalPathAsync("Snare Test.wav", TestContext.Current.CancellationToken);

        Assert.Equal(first, second);
        Assert.Equal(1, _fileSystem.PackageOpenCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetLocalPathAsync_BlankName_Throws(string name) { _ = await Assert.ThrowsAnyAsync<ArgumentException>(() => Create().GetLocalPathAsync(name, TestContext.Current.CancellationToken)); }
    [Fact]
    public async Task GetLocalPathAsync_CopiesTheAssetOutOfThePackageIntoTheCache()
    {
        byte[] bytes = AddAsset("Kick 909 DMX 1.wav");

        string path = await Create().GetLocalPathAsync("Kick 909 DMX 1.wav", TestContext.Current.CancellationToken);

        Assert.StartsWith(_fileSystem.CacheDirectory, path, StringComparison.Ordinal);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal("Kick 909 DMX 1.wav", Path.GetFileName(path));
    }

    [Fact]
    public async Task GetLocalPathAsync_MoreThanTheCacheLimit_EvictsTheLeastRecentlyUsedCopy()
    {
        SampleAssetStore store = Create();
        string folder = Path.Combine(_fileSystem.CacheDirectory, "sample-bank");
        _ = Directory.CreateDirectory(folder);
        DateTime start = DateTime.UtcNow.AddHours(-1);
        for (int i = 0; i < SampleAssetStore.MaxCachedFiles; i++)
        {
            string old = Path.Combine(folder, $"Old {i}.wav");
            await File.WriteAllBytesAsync(old, [1, 2, 3], TestContext.Current.CancellationToken);
            File.SetLastWriteTimeUtc(old, start.AddSeconds(i));
        }

        _ = AddAsset("New.wav");
        string fresh = await store.GetLocalPathAsync("New.wav", TestContext.Current.CancellationToken);

        Assert.False(File.Exists(Path.Combine(folder, "Old 0.wav")), "the oldest copy should have been evicted");
        Assert.True(File.Exists(Path.Combine(folder, $"Old {SampleAssetStore.MaxCachedFiles - 1}.wav")));
        Assert.True(File.Exists(fresh));
        Assert.Equal(SampleAssetStore.MaxCachedFiles, Directory.GetFiles(folder).Length);
    }

    [Theory]
    [InlineData("Beefy Chop F#.wav")]
    [InlineData("Break Ahmir's Voodoo 119 bpm.wav")]
    [InlineData("Hihat Closed Kaninchen.wav")]
    [InlineData("808 Oracle 10.wav")]
    public async Task GetLocalPathAsync_NamesWithPunctuation_Work(string name)
    {
        byte[] bytes = AddAsset(name);

        string path = await Create().GetLocalPathAsync(name, TestContext.Current.CancellationToken);

        Assert.Equal(bytes, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetLocalPathAsync_PlaybackCopyKeepsTheOriginalBitDepth()
    {
        byte[] file = RiffBuilder.Wav24(1, 44100, 1000, -1000);
        _fileSystem.AddPackageFile("Deep.wav", file);

        string path = await Create().GetLocalPathAsync("Deep.wav", TestContext.Current.CancellationToken);

        Assert.Equal(file, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetLocalPathAsync_UnknownAsset_ThrowsAndLeavesNoPartialFileBehind()
    {
        _ = await Assert.ThrowsAsync<FileNotFoundException>(() => Create().GetLocalPathAsync("Nope.wav", TestContext.Current.CancellationToken));

        string folder = Path.Combine(_fileSystem.CacheDirectory, "sample-bank");
        Assert.Empty(Directory.GetFiles(folder));
    }

    [Fact]
    public async Task GetLocalPathAsync_UsingACachedCopy_MakesItTheMostRecentlyUsed()
    {
        _ = AddAsset("Keep.wav");
        SampleAssetStore store = Create();
        string keep = await store.GetLocalPathAsync("Keep.wav", TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(keep, DateTime.UtcNow.AddDays(-1));

        _ = await store.GetLocalPathAsync("Keep.wav", TestContext.Current.CancellationToken);

        Assert.True(File.GetLastWriteTimeUtc(keep) > DateTime.UtcNow.AddMinutes(-1));
    }
    #endregion
}
