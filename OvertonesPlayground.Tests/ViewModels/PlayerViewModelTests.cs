namespace OvertonesPlayground.Tests.ViewModels;

public sealed class PlayerViewModelTests
{
    #region Fields
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IAudioPlaybackService _playback = Substitute.For<IAudioPlaybackService>();
    #endregion

    #region Private methods
    private PlayerViewModel Create() => new(_playback, _navigation, NullLogger<PlayerViewModel>.Instance);

    private static (IDispatcher Dispatcher, IDispatcherTimer Timer) CreateDispatcher()
    {
        IDispatcherTimer timer = Substitute.For<IDispatcherTimer>();
        IDispatcher dispatcher = Substitute.For<IDispatcher>();
        dispatcher.CreateTimer().Returns(timer);
        return (dispatcher, timer);
    }
    #endregion

    #region Public methods
    [Fact]
    public void Constructor_ClipAlreadyPlaying_PicksUpItsState()
    {
        _playback.CurrentClip.Returns(TestData.Clip("Song"));
        _playback.IsPlaying.Returns(true);
        _playback.Position.Returns(TimeSpan.FromSeconds(12));
        _playback.Duration.Returns(TimeSpan.FromSeconds(200));

        PlayerViewModel viewModel = Create();

        Assert.Equal("Song", viewModel.ClipName);
        Assert.True(viewModel.IsPlaying);
        Assert.Equal(12, viewModel.PositionSeconds);
        Assert.Equal(200, viewModel.DurationSeconds);
    }

    [Fact]
    public void Constructor_NothingLoaded_ShowsAPlaceholder()
    {
        PlayerViewModel viewModel = Create();

        Assert.Equal("Player", viewModel.Title);
        Assert.Equal("Nothing loaded", viewModel.ClipName);
        Assert.False(viewModel.IsPlaying);
        Assert.Equal(0, viewModel.PositionSeconds);
        Assert.Equal(1, viewModel.DurationSeconds);
    }

    [Fact]
    public void Dispose_StopsTheTimerAndStopsListeningToThePlaybackService()
    {
        (IDispatcher dispatcher, IDispatcherTimer timer) = CreateDispatcher();
        PlayerViewModel viewModel = Create();
        viewModel.StartTicking(dispatcher);

        viewModel.Dispose();
        _playback.CurrentClip.Returns(TestData.Clip("Ignored"));
        _playback.PlaybackStateChanged += Raise.Event();

        timer.Received(1).Stop();
        Assert.Equal("Nothing loaded", viewModel.ClipName);
    }

    [Fact]
    public void DurationSeconds_ClipShorterThanASecond_IsNeverLessThanOne()
    {
        _playback.Duration.Returns(TimeSpan.FromMilliseconds(120));

        Assert.Equal(1, Create().DurationSeconds);
    }

    [Fact]
    public void PlaybackStateChanged_RaisesTheTimeTexts()
    {
        PlayerViewModel viewModel = Create();
        List<string?> raised = [];
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _playback.PlaybackStateChanged += Raise.Event();

        Assert.Contains(nameof(PlayerViewModel.PositionText), raised);
        Assert.Contains(nameof(PlayerViewModel.DurationText), raised);
    }

    [Fact]
    public void PlaybackStateChanged_RefreshesTheViewModel()
    {
        PlayerViewModel viewModel = Create();
        _playback.CurrentClip.Returns(TestData.Clip("Next"));
        _playback.IsPlaying.Returns(true);

        _playback.PlaybackStateChanged += Raise.Event();

        Assert.Equal("Next", viewModel.ClipName);
        Assert.True(viewModel.IsPlaying);
    }

    [Fact]
    public void PlayPause_WhilePlaying_Pauses()
    {
        _playback.IsPlaying.Returns(true);

        Create().PlayPauseCommand.Execute(null);

        _playback.Received(1).Pause();
        _playback.DidNotReceive().Play();
    }

    [Fact]
    public void PlayPause_WhileStopped_Plays()
    {
        _playback.IsPlaying.Returns(false);

        Create().PlayPauseCommand.Execute(null);

        _playback.Received(1).Play();
        _playback.DidNotReceive().Pause();
    }

    [Fact]
    public void PositionAndDurationText_AreFormattedAsMinutesAndSeconds()
    {
        _playback.Position.Returns(TimeSpan.FromSeconds(65));
        _playback.Duration.Returns(TimeSpan.FromSeconds(605));

        PlayerViewModel viewModel = Create();

        Assert.Equal("01:05", viewModel.PositionText);
        Assert.Equal("10:05", viewModel.DurationText);
    }

    [Fact]
    public void Seek_MovesTheTransportToThatSecond()
    {
        Create().SeekCommand.Execute(42.5);

        _playback.Received(1).Seek(TimeSpan.FromSeconds(42.5));
    }

    [Fact]
    public void StartTicking_CalledTwice_StillUsesOneTimer()
    {
        (IDispatcher dispatcher, _) = CreateDispatcher();
        PlayerViewModel viewModel = Create();

        viewModel.StartTicking(dispatcher);
        viewModel.StartTicking(dispatcher);

        dispatcher.Received(1).CreateTimer();
    }

    [Fact]
    public void StartTicking_StartsAFastRepeatingTimerThatRefreshesTheView()
    {
        (IDispatcher dispatcher, IDispatcherTimer timer) = CreateDispatcher();
        PlayerViewModel viewModel = Create();

        viewModel.StartTicking(dispatcher);
        _playback.Position.Returns(TimeSpan.FromSeconds(7));
        timer.Tick += Raise.Event();

        Assert.Equal(TimeSpan.FromMilliseconds(200), timer.Interval);
        timer.Received(1).Start();
        Assert.Equal(7, viewModel.PositionSeconds);
    }

    [Fact]
    public void NothingLoaded_PlayAndStopAreDisabled()
    {
        PlayerViewModel viewModel = Create();

        Assert.False(viewModel.HasClip);
        Assert.False(viewModel.PlayPauseCommand.CanExecute(null));
        Assert.False(viewModel.StopCommand.CanExecute(null));
    }

    [Fact]
    public void ClipLoaded_PlayAndStopAreEnabled()
    {
        _playback.CurrentClip.Returns(TestData.Clip("Song"));

        PlayerViewModel viewModel = Create();

        Assert.True(viewModel.PlayPauseCommand.CanExecute(null));
        Assert.True(viewModel.StopCommand.CanExecute(null));
    }

    [Fact]
    public async Task PickFromLibrary_OpensTheLibrary()
    {
        await Create().PickFromLibraryCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoToAsync("//library");
    }

    [Fact]
    public void Seeking_TheTimerDoesNotPullTheSliderBack()
    {
        (IDispatcher dispatcher, IDispatcherTimer timer) = CreateDispatcher();
        _playback.CurrentClip.Returns(TestData.Clip("Song"));
        _playback.Duration.Returns(TimeSpan.FromSeconds(100));
        PlayerViewModel viewModel = Create();
        viewModel.StartTicking(dispatcher);

        viewModel.BeginSeek();
        viewModel.PreviewSeek(60);
        _playback.Position.Returns(TimeSpan.FromSeconds(5));
        timer.Tick += Raise.Event();

        Assert.Equal(60, viewModel.PositionSeconds);
        Assert.Equal("01:00", viewModel.PositionText);
    }

    [Fact]
    public void EndSeek_SeeksToTheDropPointAndResumesFollowingPlayback()
    {
        _playback.CurrentClip.Returns(TestData.Clip("Song"));
        PlayerViewModel viewModel = Create();
        viewModel.BeginSeek();

        viewModel.EndSeek(42.5);
        _playback.Position.Returns(TimeSpan.FromSeconds(43));
        _playback.PlaybackStateChanged += Raise.Event();

        _playback.Received(1).Seek(TimeSpan.FromSeconds(42.5));
        Assert.Equal(43, viewModel.PositionSeconds);
    }

    [Fact]
    public void Times_AnHourOrLonger_ShowHours()
    {
        _playback.Duration.Returns(TimeSpan.FromSeconds(3725));

        Assert.Equal("1:02:05", Create().DurationText);
    }

    [Fact]
    public void Timer_StopsWhenPlaybackStopsAndRestartsWhenItResumes()
    {
        (IDispatcher dispatcher, IDispatcherTimer timer) = CreateDispatcher();
        _playback.CurrentClip.Returns(TestData.Clip("Song"));
        _playback.IsPlaying.Returns(true);
        PlayerViewModel viewModel = Create();
        viewModel.StartTicking(dispatcher);
        bool running = true;
        timer.IsRunning.Returns(_ => running);
        timer.When(t => t.Stop()).Do(_ => running = false);
        timer.When(t => t.Start()).Do(_ => running = true);

        _playback.IsPlaying.Returns(false);
        _playback.PlaybackStateChanged += Raise.Event();
        Assert.False(running);

        _playback.IsPlaying.Returns(true);
        _playback.PlaybackStateChanged += Raise.Event();
        Assert.True(running);
    }

    [Fact]
    public void Stop_StopsTheTransport()
    {
        _playback.CurrentClip.Returns(TestData.Clip("Song"));
        Create().StopCommand.Execute(null);

        _playback.Received(1).Stop();
    }

    [Fact]
    public void StopTicking_NeverStarted_IsHarmless() { Create().StopTicking(); }
    [Fact]
    public void StopTicking_StopsTheTimer()
    {
        (IDispatcher dispatcher, IDispatcherTimer timer) = CreateDispatcher();
        PlayerViewModel viewModel = Create();
        viewModel.StartTicking(dispatcher);

        viewModel.StopTicking();

        timer.Received(1).Stop();
    }

    [Fact]
    public void Volume_Changed_IsPassedToThePlaybackService()
    {
        PlayerViewModel viewModel = Create();

        viewModel.Volume = 0.25;

        _playback.Received().Volume = 0.25;
    }
    #endregion
}
