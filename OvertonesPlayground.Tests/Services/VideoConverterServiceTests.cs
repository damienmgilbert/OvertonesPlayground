using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Tests.Services;

public sealed class VideoConverterServiceTests : IDisposable
{
    #region Fields
    private readonly IAudioFormatConverterService _converter = Substitute.For<IAudioFormatConverterService>();
    private readonly TempFileSystem _files = new();
    private readonly IFilePicker _picker = Substitute.For<IFilePicker>();
    private readonly IPublicStorageService _publicStorage = Substitute.For<IPublicStorageService>();
    #endregion

    #region Private methods
    private VideoConverterService Create() => new(_picker, _files, _converter, _publicStorage);

    private FileResult PickedVideoInTheCache(string name = "clip.mp4", string contents = "video")
    {
        string path = Path.Combine(_files.CacheDirectory, name);
        File.WriteAllText(path, contents);
        FileResult result = new(path);
        _picker.PickAsync(Arg.Any<PickOptions>()).Returns(result);
        return result;
    }
    #endregion

    #region Public methods
    [Fact]
    public async Task Convert_AsksForAnMp4Video()
    {
        _picker.PickAsync(Arg.Any<PickOptions>()).Returns((FileResult?)null);

        await Create().ConvertFromPickerAsync(VideoConversionFormat.Wav);

        await _picker.Received(1).PickAsync(Arg.Is<PickOptions>(options => options.PickerTitle == "Choose an MP4 video"));
    }

    [Fact]
    public async Task Convert_DecodeFails_TheWorkingCopyIsStillRemoved()
    {
        PickedVideoInTheCache();
        _converter.ConvertToWavAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<double>>())
            .Returns(Task.FromException<string>(new NotSupportedException("no audio track")));

        await Assert.ThrowsAsync<NotSupportedException>(() => Create().ConvertFromPickerAsync(VideoConversionFormat.Wav));

        string workingDirectory = Path.Combine(_files.CacheDirectory, "VideoImports");
        Assert.Empty(Directory.GetFiles(workingDirectory));
    }

    [Fact]
    public async Task Convert_Mp3_DecodesThenEncodesThenExports()
    {
        PickedVideoInTheCache("Movie.mp4");
        _converter.ConvertToWavAsync(Arg.Any<string>(), "Movie", Arg.Any<IProgress<double>>()).Returns("/decoded/movie.wav");
        _converter.ConvertFromWavAsync("/decoded/movie.wav", AudioExportFormat.Mp3, "Movie").Returns("/encoded/movie.mp3");
        _publicStorage.ExportToMusicAsync("/encoded/movie.mp3", "movie.mp3").Returns("Music/movie.mp3");

        VideoConversionOutcome? outcome = await Create().ConvertFromPickerAsync(VideoConversionFormat.Mp3);

        Assert.Equal("movie.mp3", outcome!.Value.FileName);
        Assert.Equal("Music/movie.mp3", outcome.Value.PublicLocation);
    }

    [Fact]
    public async Task Convert_PickerCancelled_ReturnsNull()
    {
        _picker.PickAsync(Arg.Any<PickOptions>()).Returns((FileResult?)null);

        Assert.Null(await Create().ConvertFromPickerAsync(VideoConversionFormat.Wav));
        await _converter.DidNotReceiveWithAnyArgs().ConvertToWavAsync(default!, default!);
    }

    [Fact]
    public async Task Convert_ReportsStagesInOrder_ForMp3()
    {
        PickedVideoInTheCache();
        _converter.ConvertToWavAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<double>>())
            .Returns(
        call =>
        {
            call.Arg<IProgress<double>>().Report(0.5);
            return Task.FromResult("/decoded.wav");
        });
        _converter.ConvertFromWavAsync(Arg.Any<string>(), AudioExportFormat.Mp3, Arg.Any<string>()).Returns("/encoded.mp3");
        RecordingProgress progress = new();

        await Create().ConvertFromPickerAsync(VideoConversionFormat.Mp3, progress);

        Assert.Equal([ImportStage.Copying, ImportStage.Decoding, ImportStage.Encoding, ImportStage.Encoding, ImportStage.Finishing], progress.Reports.Select(r => r.Stage));
        Assert.All(progress.Reports, report => Assert.InRange(report.Fraction, 0, 1));
    }

    [Fact]
    public async Task Convert_ReportsStagesInOrder_ForWav()
    {
        PickedVideoInTheCache();
        _converter.ConvertToWavAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IProgress<double>>())
            .Returns(
        call =>
        {
            call.Arg<IProgress<double>>().Report(0.5);
            return Task.FromResult("/decoded.wav");
        });
        RecordingProgress progress = new();

        await Create().ConvertFromPickerAsync(VideoConversionFormat.Wav, progress);

        Assert.Equal([ImportStage.Copying, ImportStage.Decoding, ImportStage.Finishing], progress.Reports.Select(r => r.Stage));
        Assert.All(progress.Reports, report => Assert.InRange(report.Fraction, 0, 1));
    }

    [Fact]
    public async Task Convert_SharedStorageRefuses_StillReturnsTheFileName()
    {
        PickedVideoInTheCache("Movie.mp4");
        _converter.ConvertToWavAsync(Arg.Any<string>(), "Movie", Arg.Any<IProgress<double>>()).Returns("/decoded/movie.wav");
        _publicStorage.ExportToMusicAsync(default!, default!).ReturnsForAnyArgs((string?)null);

        VideoConversionOutcome? outcome = await Create().ConvertFromPickerAsync(VideoConversionFormat.Wav);

        Assert.Equal("movie.wav", outcome!.Value.FileName);
        Assert.Null(outcome.Value.PublicLocation);
    }

    [Fact]
    public async Task Convert_Wav_DecodesTheVideoAndExportsItDirectly()
    {
        PickedVideoInTheCache("Movie.mp4");
        _converter.ConvertToWavAsync(Arg.Any<string>(), "Movie", Arg.Any<IProgress<double>>()).Returns("/decoded/movie.wav");
        _publicStorage.ExportToMusicAsync("/decoded/movie.wav", "movie.wav").Returns("Music/movie.wav");

        VideoConversionOutcome? outcome = await Create().ConvertFromPickerAsync(VideoConversionFormat.Wav);

        Assert.Equal("movie.wav", outcome!.Value.FileName);
        Assert.Equal("Music/movie.wav", outcome.Value.PublicLocation);
        await _converter.DidNotReceiveWithAnyArgs().ConvertFromWavAsync(default!, default, default!);
    }

    public void Dispose() => _files.Dispose();
    #endregion

    #region Nested types
    private sealed class RecordingProgress : IProgress<ImportProgress>
    {
        #region Public methods
        public void Report(ImportProgress value) => Reports.Add(value);
        #endregion

        #region Public properties
        public List<ImportProgress> Reports { get; } = [];
        #endregion
    }
    #endregion
}
