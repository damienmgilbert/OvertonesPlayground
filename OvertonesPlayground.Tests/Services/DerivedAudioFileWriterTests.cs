using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Tests.Services;

public sealed class DerivedAudioFileWriterTests : IDisposable
{
    private readonly TempFileSystem _files = new();

    public void Dispose() => _files.Dispose();

    private static WavFile Wav(params short[] samples) => new() { Channels = 1, SampleRate = 8000, BitsPerSample = 16, Samples = samples };

    [Fact]
    public async Task SaveAsync_WritesAReadableWavIntoTheFolderCreatingItIfNeeded()
    {
        string directory = _files.InAppData("does", "not", "exist");

        string path = await DerivedAudioFileWriter.SaveAsync(Wav(1, 2, 3), directory, "clip");

        Assert.Equal(directory, Path.GetDirectoryName(path));
        Assert.EndsWith(".wav", path);
        Assert.StartsWith("clip_", Path.GetFileName(path));
        Assert.Equal([1, 2, 3], (await WavFile.ReadAsync(path)).Samples);
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("what?:*")]
    public async Task SaveAsync_NameWithForbiddenCharacters_CannotEscapeTheFolder(string name)
    {
        string directory = _files.InAppData("out");

        string path = await DerivedAudioFileWriter.SaveAsync(Wav(1), directory, name);

        Assert.Equal(directory, Path.GetDirectoryName(path));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task SaveBytesAsync_WritesTheBytesWithTheGivenExtension()
    {
        string path = await DerivedAudioFileWriter.SaveBytesAsync([1, 2, 3, 4], _files.InAppData("out"), "encoded", "m4a");

        Assert.EndsWith(".m4a", path);
        Assert.Equal([1, 2, 3, 4], await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveAsync_SameNameSavedRepeatedlyInQuickSuccession_NeverOverwritesAnEarlierFile()
    {
        // Every derived file is named after what was done to it plus the time to the second, so "gain" applied twice in a row
        // would otherwise write to the same path and replace a clip the library already points at.
        string directory = _files.InAppData("out");
        List<string> paths = [];

        for (short i = 1; i <= 6; i++)
        {
            paths.Add(await DerivedAudioFileWriter.SaveAsync(Wav(i), directory, "gain"));
        }

        Assert.Equal(6, paths.Distinct().Count());
        for (int i = 0; i < paths.Count; i++)
        {
            Assert.Equal([(short)(i + 1)], (await WavFile.ReadAsync(paths[i])).Samples);
        }
    }

    [Fact]
    public async Task SaveBytesAsync_SameNameSavedRepeatedly_NeverOverwritesAnEarlierFile()
    {
        string directory = _files.InAppData("out");

        string first = await DerivedAudioFileWriter.SaveBytesAsync([1], directory, "export", "m4a");
        string second = await DerivedAudioFileWriter.SaveBytesAsync([2], directory, "export", "m4a");

        Assert.NotEqual(first, second);
        Assert.Equal([1], await File.ReadAllBytesAsync(first, TestContext.Current.CancellationToken));
        Assert.Equal([2], await File.ReadAllBytesAsync(second, TestContext.Current.CancellationToken));
    }
}
