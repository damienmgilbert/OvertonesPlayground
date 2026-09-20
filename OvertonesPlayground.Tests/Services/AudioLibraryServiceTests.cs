using OvertonesPlayground.Services.Implementations;
using Plugin.Maui.Audio;

namespace OvertonesPlayground.Tests.Services;

public sealed class AudioLibraryServiceTests : IDisposable
{
    #region Fields
    private readonly IAudioManager _audioManager = Substitute.For<IAudioManager>();
    private readonly IAudioFormatConverterService _converter = Substitute.For<IAudioFormatConverterService>();
    private readonly TempFileSystem _files = new();
    private readonly IFilePicker _picker = Substitute.For<IFilePicker>();
    private readonly IPublicStorageService _publicStorage = Substitute.For<IPublicStorageService>();
    #endregion

    #region Private methods
    private AudioLibraryService Create() => new(_audioManager, _converter, _publicStorage, _files, _picker);

    private FileResult PickedFileInTheCache(string name, string contents = "audio")
    {
        string path = Path.Combine(_files.CacheDirectory, name);
        File.WriteAllText(path, contents);
        _picker.PickAsync(Arg.Any<PickOptions>()).Returns(new FileResult(path));
        return new FileResult(path);
    }

    private void PlayersReportDuration(double seconds)
    {
        IAudioPlayer player = Substitute.For<IAudioPlayer>();
        player.Duration.Returns(seconds);
        _audioManager.CreatePlayer(Arg.Any<string>()).Returns(player);
    }
    #endregion

    #region Public methods
    [Fact]
    public async Task AddClip_ImportedFile_IsNotSentToSharedStorage()
    {
        PlayersReportDuration(1);

        await Create().AddClipAsync("/audio/song.wav", "Song");

        await _publicStorage.DidNotReceiveWithAnyArgs().ExportToMusicAsync(default!, default!);
    }

    [Fact]
    public async Task AddClip_PlayerCantOpenTheFile_StillAddsItWithNoDuration()
    {
        _audioManager.CreatePlayer(Arg.Any<string>()).Returns(_ => throw new InvalidOperationException("bad file"));

        AudioClip clip = await Create().AddClipAsync("/audio/broken.wav", "Broken");

        Assert.Equal(TimeSpan.Zero, clip.Duration);
        Assert.Single(await Create().GetClipsAsync());
    }

    [Fact]
    public async Task AddClip_TakesItsDurationFromThePlayer()
    {
        PlayersReportDuration(3.5);

        AudioClip clip = await Create().AddClipAsync("/audio/song.wav", "Song");

        Assert.Equal("Song", clip.Name);
        Assert.Equal("/audio/song.wav", clip.FilePath);
        Assert.Equal(TimeSpan.FromSeconds(3.5), clip.Duration);
        Assert.False(clip.IsUserRecording);
        Assert.Null(clip.PublicStorageLocation);
    }

    [Fact]
    public async Task AddClip_UserRecording_IsAlsoCopiedToTheSharedMusicFolder()
    {
        PlayersReportDuration(1);
        _publicStorage.ExportToMusicAsync("/audio/take.wav", "take.wav").Returns("Music/take.wav");

        AudioClip clip = await Create().AddClipAsync("/audio/take.wav", "Take", isUserRecording: true);

        Assert.True(clip.IsUserRecording);
        Assert.Equal("Music/take.wav", clip.PublicStorageLocation);
    }

    [Fact]
    public async Task AddClip_UserRecordingThatCantBeExported_IsStillSaved()
    {
        PlayersReportDuration(1);
        _publicStorage.ExportToMusicAsync(default!, default!).ReturnsForAnyArgs((string?)null);

        AudioClip clip = await Create().AddClipAsync("/audio/take.wav", "Take", isUserRecording: true);

        Assert.Null(clip.PublicStorageLocation);
        Assert.Single(await Create().GetClipsAsync());
    }

    [Fact]
    public async Task DeleteClip_FileAlreadyGone_StillRemovesTheEntry()
    {
        PlayersReportDuration(1);
        AudioLibraryService service = Create();
        AudioClip clip = await service.AddClipAsync(_files.InAppData("gone.wav"), "Gone");

        await service.DeleteClipAsync(clip);

        Assert.Empty(await service.GetClipsAsync());
    }

    [Fact]
    public async Task DeleteClip_NotInTheLibrary_ChangesNothing()
    {
        PlayersReportDuration(1);
        AudioLibraryService service = Create();
        await service.AddClipAsync("/a.wav", "A");

        await service.DeleteClipAsync(TestData.Clip("Stranger"));

        Assert.Single(await service.GetClipsAsync());
    }

    [Fact]
    public async Task DeleteClip_RemovesItFromTheLibraryAndDeletesItsFile()
    {
        PlayersReportDuration(1);
        string file = _files.CreateFile("clips/song.wav");
        AudioLibraryService service = Create();
        AudioClip keep = await service.AddClipAsync("/keep.wav", "Keep");
        AudioClip drop = await service.AddClipAsync(file, "Drop");

        await service.DeleteClipAsync(drop);

        Assert.Equal([keep.Id], (await Create().GetClipsAsync()).Select(c => c.Id));
        Assert.False(File.Exists(file));
    }

    public void Dispose() => _files.Dispose();

    [Theory]
    [InlineData(AudioExportFormat.Aac)]
    [InlineData(AudioExportFormat.Mp3)]
    public async Task Export_EncodesTheClipThenCopiesItToSharedStorage(AudioExportFormat format)
    {
        AudioClip clip = TestData.Clip("Song", path: "/audio/song.wav");
        _converter.ConvertFromWavAsync("/audio/song.wav", format, "Song").Returns("/encoded/song.out");
        _publicStorage.ExportToMusicAsync("/encoded/song.out", "song.out").Returns("Music/song.out");

        string? location = await Create().ExportClipAsync(clip, format);

        Assert.Equal("Music/song.out", location);
    }

    [Fact]
    public async Task Export_NoClip_Throws() { await Assert.ThrowsAsync<ArgumentNullException>(() => Create().ExportClipAsync(null!, AudioExportFormat.Aac)); }
    [Fact]
    public async Task Export_SharedStorageRefuses_ReturnsNull()
    {
        _converter.ConvertFromWavAsync(default!, default, default!).ReturnsForAnyArgs("/encoded/song.out");
        _publicStorage.ExportToMusicAsync(default!, default!).ReturnsForAnyArgs((string?)null);

        Assert.Null(await Create().ExportClipAsync(TestData.Clip("Song"), AudioExportFormat.Aac));
    }

    [Fact]
    public async Task GetClips_ListsTheNewestFirst()
    {
        PlayersReportDuration(1);
        AudioLibraryService service = Create();
        await service.AddClipAsync("/a.wav", "First");
        await Task.Delay(20, TestContext.Current.CancellationToken);
        await service.AddClipAsync("/b.wav", "Second");
        await Task.Delay(20, TestContext.Current.CancellationToken);
        await service.AddClipAsync("/c.wav", "Third");

        IReadOnlyList<AudioClip> clips = await service.GetClipsAsync();

        Assert.Equal(["Third", "Second", "First"], clips.Select(c => c.Name));
    }

    [Fact]
    public async Task GetClips_NothingAddedYet_IsEmpty() { Assert.Empty(await Create().GetClipsAsync()); }
    [Fact]
    public async Task Import_AsksForAnAudioFile()
    {
        _picker.PickAsync(Arg.Any<PickOptions>()).Returns((FileResult?)null);

        await Create().ImportFromPickerAsync();

        await _picker.Received(1).PickAsync(Arg.Is<PickOptions>(options => options.PickerTitle == "Choose an audio file"));
    }

    [Fact]
    public async Task Import_CompressedFile_IsDecodedToWavAndTheOriginalRemoved()
    {
        PlayersReportDuration(1);
        PickedFileInTheCache("Song.mp3");
        string wavPath = _files.CreateFile("Clips/decoded.wav");
        string? convertedFrom = null;
        _converter.NeedsConversion(Arg.Any<string>()).Returns(true);
        _converter.ConvertToWavAsync(Arg.Any<string>(), "Song", Arg.Any<IProgress<double>>())
            .Returns(
        call =>
        {
            convertedFrom = call.ArgAt<string>(0);
            call.Arg<IProgress<double>>().Report(0.5);
            return Task.FromResult(wavPath);
        });
        RecordingProgress progress = new();

        AudioClip? clip = await Create().ImportFromPickerAsync(progress);

        Assert.Equal(wavPath, clip!.FilePath);
        Assert.NotNull(convertedFrom);
        Assert.False(File.Exists(convertedFrom), "the compressed copy should be deleted once it has been decoded");
        Assert.Equal([ImportStage.Copying, ImportStage.Decoding, ImportStage.Finishing], progress.Reports.Select(r => r.Stage));
        Assert.Equal(0.1, progress.Reports[0].Fraction, 6); // decoding is slow, so copying only gets the first tenth
        Assert.Equal(0.1 + (0.5 * (0.95 - 0.1)), progress.Reports[1].Fraction, 6);
    }

    [Fact]
    public async Task Import_FileTheSystemPickerCopiedIntoTheCache_IsMovedIntoTheLibrary()
    {
        PlayersReportDuration(4);
        FileResult picked = PickedFileInTheCache("Song.wav", "the audio");

        AudioClip? clip = await Create().ImportFromPickerAsync();

        Assert.NotNull(clip);
        Assert.Equal("Song", clip.Name);
        Assert.Equal(TimeSpan.FromSeconds(4), clip.Duration);
        Assert.Equal(_files.InAppData("Clips"), Path.GetDirectoryName(clip.FilePath));
        Assert.EndsWith("_Song.wav", clip.FilePath);
        Assert.Equal("the audio", await File.ReadAllTextAsync(clip.FilePath, TestContext.Current.CancellationToken));
        Assert.False(File.Exists(picked.FullPath), "the cache copy should have been moved, not copied");
        Assert.Equal([clip.Id], (await Create().GetClipsAsync()).Select(c => c.Id));
    }

    [Fact]
    public async Task Import_PickedFileOutsideTheCache_IsNeverMoved()
    {
        // A file the user picked from their own storage isn't a picker copy, so it must be left where it is.
        string usersOwnFile = _files.CreateFile("Music/mine.wav", "precious");
        _picker.PickAsync(Arg.Any<PickOptions>()).Returns(new FileResult(usersOwnFile));

        // It has to be copied instead. The host has no platform stream to copy from, so it fails at that step - which is how this
        // shows the import took the copy path and not the move path - but the original stays put.
        Exception copyAttempt = await Assert.ThrowsAnyAsync<Exception>(() => Create().ImportFromPickerAsync());
        Assert.Equal("NotImplementedInReferenceAssemblyException", copyAttempt.GetType().Name);

        Assert.Equal("precious", await File.ReadAllTextAsync(usersOwnFile, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Import_PickerCancelled_ImportsNothing()
    {
        _picker.PickAsync(Arg.Any<PickOptions>()).Returns((FileResult?)null);

        Assert.Null(await Create().ImportFromPickerAsync());
        Assert.Empty(await Create().GetClipsAsync());
    }

    [Fact]
    public async Task Import_ReportsCopyingThenFinishingInOrder()
    {
        PlayersReportDuration(1);
        PickedFileInTheCache("Song.wav");
        RecordingProgress progress = new();

        await Create().ImportFromPickerAsync(progress);

        Assert.Equal([ImportStage.Copying, ImportStage.Finishing], progress.Reports.Select(r => r.Stage));
        Assert.Equal(0.95, progress.Reports[0].Fraction, 6); // a plain WAV is nearly all copying
        Assert.Equal(0.95, progress.Reports[1].Fraction, 6);
        Assert.All(progress.Reports, report => Assert.InRange(report.Fraction, 0, 1));
    }

    [Fact]
    public async Task Import_TwoFilesWithTheSameName_DoNotCollide()
    {
        PlayersReportDuration(1);
        PickedFileInTheCache("Song.wav", "one");
        AudioLibraryService service = Create();
        AudioClip first = (await service.ImportFromPickerAsync())!;
        PickedFileInTheCache("Song.wav", "two");

        AudioClip second = (await service.ImportFromPickerAsync())!;

        Assert.NotEqual(first.FilePath, second.FilePath);
        Assert.Equal("one", await File.ReadAllTextAsync(first.FilePath, TestContext.Current.CancellationToken));
        Assert.Equal("two", await File.ReadAllTextAsync(second.FilePath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Library_IsKeptOnDiskAndReadBackByANewInstance()
    {
        PlayersReportDuration(2);
        AudioClip added = await Create().AddClipAsync("/audio/song.wav", "Song");

        IReadOnlyList<AudioClip> loaded = await Create().GetClipsAsync();

        Assert.True(File.Exists(_files.InAppData("library.json")));
        AudioClip clip = Assert.Single(loaded);
        Assert.Equal(added.Id, clip.Id);
        Assert.Equal("Song", clip.Name);
        Assert.Equal(TimeSpan.FromSeconds(2), clip.Duration);
    }

    [Fact]
    public async Task RenameClip_ChangesTheNameAndKeepsIt()
    {
        PlayersReportDuration(1);
        AudioLibraryService service = Create();
        AudioClip clip = await service.AddClipAsync("/a.wav", "Old");

        await service.RenameClipAsync(clip, "New");

        Assert.Equal("New", (await Create().GetClipsAsync()).Single().Name);
    }

    [Fact]
    public async Task RenameClip_NotInTheLibrary_ChangesNothing()
    {
        PlayersReportDuration(1);
        AudioLibraryService service = Create();
        await service.AddClipAsync("/a.wav", "A");

        await service.RenameClipAsync(TestData.Clip("Stranger"), "New");

        Assert.Equal("A", (await service.GetClipsAsync()).Single().Name);
    }
    #endregion

    private sealed class RecordingProgress : IProgress<ImportProgress>
    {
        #region Public methods
        public void Report(ImportProgress value) => Reports.Add(value);
        #endregion

        #region Public properties
        public List<ImportProgress> Reports { get; } = [];
        #endregion
    }
}
