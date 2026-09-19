using CommunityToolkit.Mvvm.Input;

namespace OvertonesPlayground.Tests.ViewModels;

public sealed class AudioEditorViewModelTests
{
    private readonly IAudioEditorService _editor = Substitute.For<IAudioEditorService>();
    private readonly IAudioLibraryService _library = Substitute.For<IAudioLibraryService>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IAudioPlaybackService _playback = Substitute.For<IAudioPlaybackService>();

    private AudioEditorViewModel Create() => new(_editor, _library, _playback, _navigation, NullLogger<AudioEditorViewModel>.Instance);

    /// <summary>
    /// A view model with <paramref name="clip"/> already loaded into it, the way the page does it: by setting the clip id.
    /// </summary>
    private AudioEditorViewModel Loaded(AudioClip clip)
    {
        _library.GetClipsAsync().Returns(TestData.Clips(clip));
        AudioEditorViewModel viewModel = Create();
        viewModel.ClipId = clip.Id;
        return viewModel;
    }

    /// <summary>
    /// Makes the library hand back <paramref name="saved"/> when the edit's output file is added to it.
    /// </summary>
    private void LibraryWillSave(string outputPath, AudioClip saved) => _library.AddClipAsync(outputPath, Arg.Any<string>(), true).Returns(saved);

    #region Loading a clip
    [Fact]
    public void ClipId_Set_LoadsTheClipIntoTheEditor()
    {
        AudioClip clip = TestData.Clip("Song", seconds: 12.5);
        _editor.GetWaveformPeaksAsync(clip.FilePath, 400).Returns([0.1f, 0.9f]);

        AudioEditorViewModel viewModel = Loaded(clip);

        Assert.Same(clip, viewModel.LoadedClip);
        Assert.Equal(12.5, viewModel.DurationSeconds);
        Assert.Equal(0, viewModel.TrimStartSeconds);
        Assert.Equal(12.5, viewModel.TrimEndSeconds);
        Assert.Equal([0.1f, 0.9f], viewModel.WaveformPeaks);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public void ClipId_NamesAClipThatIsntInTheLibrary_SaysSo()
    {
        _library.GetClipsAsync().Returns(TestData.Clips(TestData.Clip("Other")));
        AudioEditorViewModel viewModel = Create();

        viewModel.ClipId = "missing";

        Assert.Null(viewModel.LoadedClip);
        Assert.Equal("Could not find that clip.", viewModel.StatusMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public void ClipId_ClipCantBeRead_SaysSo()
    {
        AudioClip clip = TestData.Clip("Broken");
        _library.GetClipsAsync().Returns(TestData.Clips(clip));
        _editor.GetWaveformPeaksAsync(clip.FilePath, 400).Returns<float[]>(_ => throw new InvalidDataException());
        AudioEditorViewModel viewModel = Create();

        viewModel.ClipId = clip.Id;

        Assert.Equal("Couldn't load that clip.", viewModel.StatusMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ClipId_ClearedOrEmpty_LoadsNothing(string? id)
    {
        AudioEditorViewModel viewModel = Create();

        viewModel.ClipId = id;

        _library.DidNotReceive().GetClipsAsync();
    }
    #endregion

    #region Editing
    [Fact]
    public async Task ApplyGain_SavesTheResultAsANewClipAndOpensIt()
    {
        AudioClip clip = TestData.Clip("Song");
        AudioClip edited = TestData.Clip("Song (edited)", seconds: 9);
        _editor.ApplyGainAsync(clip.FilePath, 4.5, "gain").Returns("gain.wav");
        LibraryWillSave("gain.wav", edited);
        AudioEditorViewModel viewModel = Loaded(clip);
        viewModel.GainDb = 4.5;

        await viewModel.ApplyGainCommand.ExecuteAsync(null);

        await _library.Received(1).AddClipAsync("gain.wav", "Song (edited)", true);
        Assert.Same(edited, viewModel.LoadedClip);
        Assert.Equal(9, viewModel.DurationSeconds);
        Assert.Equal("Saved as 'Song (edited)' in your library.", viewModel.StatusMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task ApplyGain_ResultAlsoExported_NamesTheSharedLocation()
    {
        AudioClip clip = TestData.Clip("Song");
        _editor.ApplyGainAsync(clip.FilePath, 0, "gain").Returns("gain.wav");
        LibraryWillSave("gain.wav", TestData.Clip("Song (edited)", publicLocation: "Music/Song.wav"));
        AudioEditorViewModel viewModel = Loaded(clip);

        await viewModel.ApplyGainCommand.ExecuteAsync(null);

        Assert.Equal("Saved as 'Song (edited)' - also in Music/Song.wav.", viewModel.StatusMessage);
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(InvalidDataException))]
    [InlineData(typeof(NotSupportedException))]
    public async Task ApplyGain_EditFails_ReportsItAndKeepsTheOriginalClip(Type exceptionType)
    {
        AudioClip clip = TestData.Clip("Song");
        _editor.ApplyGainAsync(clip.FilePath, Arg.Any<double>(), "gain").Returns<string>(_ => throw (Exception)Activator.CreateInstance(exceptionType)!);
        AudioEditorViewModel viewModel = Loaded(clip);

        await viewModel.ApplyGainCommand.ExecuteAsync(null);

        Assert.Equal("Couldn't apply that gain.", viewModel.StatusMessage);
        Assert.Same(clip, viewModel.LoadedClip);
        Assert.False(viewModel.IsBusy);
        await _library.DidNotReceiveWithAnyArgs().AddClipAsync(default!, default!, default);
    }

    [Fact]
    public async Task Editing_NoClipLoaded_DoesNothing()
    {
        AudioEditorViewModel viewModel = Create();

        await viewModel.ApplyGainCommand.ExecuteAsync(null);
        await viewModel.NormalizeCommand.ExecuteAsync(null);
        await viewModel.ReverseCommand.ExecuteAsync(null);
        await viewModel.TrimCommand.ExecuteAsync(null);

        Assert.Empty(_editor.ReceivedCalls());
    }

    [Fact]
    public async Task Editing_WhileAnotherEditIsRunning_IsIgnored()
    {
        AudioClip clip = TestData.Clip("Song");
        AudioEditorViewModel viewModel = Loaded(clip);
        viewModel.IsBusy = true;

        await viewModel.ApplyGainCommand.ExecuteAsync(null);

        await _editor.DidNotReceiveWithAnyArgs().ApplyGainAsync(default!, default, default!);
    }

    [Fact]
    public async Task ApplyCompression_UsesTheCompressorSettings()
    {
        AudioClip clip = TestData.Clip("Song");
        _editor.ApplyCompressionAsync(clip.FilePath, -24, 8, 5, 250, "compress").Returns("out.wav");
        LibraryWillSave("out.wav", TestData.Clip("Song (edited)"));
        AudioEditorViewModel viewModel = Loaded(clip);
        viewModel.CompressionThresholdDb = -24;
        viewModel.CompressionRatio = 8;
        viewModel.CompressionAttackMs = 5;
        viewModel.CompressionReleaseMs = 250;

        await viewModel.ApplyCompressionCommand.ExecuteAsync(null);

        await _library.Received(1).AddClipAsync("out.wav", "Song (edited)", true);
    }

    [Fact]
    public void CompressionSettings_StartAtTheDocumentedDefaults()
    {
        AudioEditorViewModel viewModel = Create();

        Assert.Equal(-18, viewModel.CompressionThresholdDb);
        Assert.Equal(4, viewModel.CompressionRatio);
        Assert.Equal(10, viewModel.CompressionAttackMs);
        Assert.Equal(100, viewModel.CompressionReleaseMs);
    }

    [Fact]
    public async Task ApplyEqualizer_UsesTheThreeBandGains()
    {
        AudioClip clip = TestData.Clip("Song");
        _editor.ApplyEqualizerAsync(clip.FilePath, 3, -2, 6, "eq").Returns("out.wav");
        LibraryWillSave("out.wav", TestData.Clip("Song (edited)"));
        AudioEditorViewModel viewModel = Loaded(clip);
        viewModel.EqLowGainDb = 3;
        viewModel.EqMidGainDb = -2;
        viewModel.EqHighGainDb = 6;

        await viewModel.ApplyEqualizerCommand.ExecuteAsync(null);

        await _library.Received(1).AddClipAsync("out.wav", "Song (edited)", true);
    }

    [Fact]
    public async Task ApplyFade_UsesTheFadeInAndOutLengths()
    {
        AudioClip clip = TestData.Clip("Song");
        _editor.ApplyFadeAsync(clip.FilePath, TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(0.25), "fade").Returns("out.wav");
        LibraryWillSave("out.wav", TestData.Clip("Song (edited)"));
        AudioEditorViewModel viewModel = Loaded(clip);
        viewModel.FadeInSeconds = 1.5;
        viewModel.FadeOutSeconds = 0.25;

        await viewModel.ApplyFadeCommand.ExecuteAsync(null);

        await _library.Received(1).AddClipAsync("out.wav", "Song (edited)", true);
    }

    [Fact]
    public async Task ApplyVoiceChange_UsesTheSemitoneShift()
    {
        AudioClip clip = TestData.Clip("Song");
        _editor.ApplyVoiceChangeAsync(clip.FilePath, -5, "voice-change").Returns("out.wav");
        LibraryWillSave("out.wav", TestData.Clip("Song (edited)"));
        AudioEditorViewModel viewModel = Loaded(clip);
        viewModel.VoiceChangeSemitones = -5;

        await viewModel.ApplyVoiceChangeCommand.ExecuteAsync(null);

        await _library.Received(1).AddClipAsync("out.wav", "Song (edited)", true);
    }

    [Fact]
    public async Task ReduceNoise_UsesTheNoiseSampleLength()
    {
        AudioClip clip = TestData.Clip("Song");
        _editor.ReduceNoiseAsync(clip.FilePath, TimeSpan.FromSeconds(0.75), "denoise").Returns("out.wav");
        LibraryWillSave("out.wav", TestData.Clip("Song (edited)"));
        AudioEditorViewModel viewModel = Loaded(clip);
        viewModel.NoiseSampleSeconds = 0.75;

        await viewModel.ReduceNoiseCommand.ExecuteAsync(null);

        await _library.Received(1).AddClipAsync("out.wav", "Song (edited)", true);
    }

    [Fact]
    public async Task Trim_UsesTheSelectedRange()
    {
        AudioClip clip = TestData.Clip("Song", seconds: 10);
        _editor.TrimAsync(clip.FilePath, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(7), "trim").Returns("out.wav");
        LibraryWillSave("out.wav", TestData.Clip("Song (edited)"));
        AudioEditorViewModel viewModel = Loaded(clip);
        viewModel.TrimStartSeconds = 2;
        viewModel.TrimEndSeconds = 7;

        await viewModel.TrimCommand.ExecuteAsync(null);

        await _library.Received(1).AddClipAsync("out.wav", "Song (edited)", true);
    }

    [Theory]
    [InlineData("normalize")]
    [InlineData("no-vocals")]
    [InlineData("reverse")]
    public async Task SimpleEdits_RunTheirOperationOnTheLoadedClip(string outputName)
    {
        AudioClip clip = TestData.Clip("Song");
        _editor.NormalizeAsync(clip.FilePath, "normalize").Returns("normalize.wav");
        _editor.RemoveVocalsAsync(clip.FilePath, "no-vocals").Returns("no-vocals.wav");
        _editor.ReverseAsync(clip.FilePath, "reverse").Returns("reverse.wav");
        LibraryWillSave($"{outputName}.wav", TestData.Clip("Song (edited)"));
        AudioEditorViewModel viewModel = Loaded(clip);

        IAsyncRelayCommand command = outputName switch
        {
            "normalize" => viewModel.NormalizeCommand,
            "no-vocals" => viewModel.RemoveVocalsCommand,
            _ => viewModel.ReverseCommand,
        };
        await command.ExecuteAsync(null);

        await _library.Received(1).AddClipAsync($"{outputName}.wav", "Song (edited)", true);
    }
    #endregion

    #region Preview and navigation
    [Fact]
    public async Task Preview_LoadsTheClipAndPlaysIt()
    {
        AudioClip clip = TestData.Clip("Song");
        AudioEditorViewModel viewModel = Loaded(clip);

        await viewModel.PreviewCommand.ExecuteAsync(null);

        Received.InOrder(() =>
        {
            _playback.LoadAsync(clip);
            _playback.Play();
        });
    }

    [Fact]
    public async Task Preview_NoClipLoaded_PlaysNothing()
    {
        await Create().PreviewCommand.ExecuteAsync(null);

        _playback.DidNotReceive().Play();
    }

    [Fact]
    public async Task OpenTrimEditor_NavigatesToTheTrimPageForTheLoadedClip()
    {
        AudioClip clip = TestData.Clip("Song");
        AudioEditorViewModel viewModel = Loaded(clip);

        await viewModel.OpenTrimEditorCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync($"trim?clipId={clip.Id}");
    }

    [Fact]
    public async Task OpenTrimEditor_NoClipLoaded_DoesNotNavigate()
    {
        await Create().OpenTrimEditorCommand.ExecuteAsync(null);

        await _navigation.DidNotReceiveWithAnyArgs().GoToAsync(default!);
    }
    #endregion
}
