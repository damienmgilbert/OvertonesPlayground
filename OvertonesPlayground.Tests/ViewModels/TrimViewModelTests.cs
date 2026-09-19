namespace OvertonesPlayground.Tests.ViewModels;

public sealed class TrimViewModelTests
{
    private readonly IAudioEditorService _editor = Substitute.For<IAudioEditorService>();
    private readonly IAudioLibraryService _library = Substitute.For<IAudioLibraryService>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IAudioPlaybackService _playback = Substitute.For<IAudioPlaybackService>();

    private TrimViewModel Create() => new(_editor, _library, _playback, _navigation, NullLogger<TrimViewModel>.Instance);

    /// <summary>
    /// A view model with a clip of <paramref name="seconds"/> already loaded, the way the page does it: by setting the clip id.
    /// </summary>
    private TrimViewModel Loaded(double seconds = 100, string name = "Song")
    {
        AudioClip clip = TestData.Clip(name, seconds);
        _library.GetClipsAsync().Returns(TestData.Clips(clip));
        TrimViewModel viewModel = Create();
        viewModel.ClipId = clip.Id;
        return viewModel;
    }

    private static (IDispatcher Dispatcher, IDispatcherTimer Timer) CreateDispatcher()
    {
        IDispatcherTimer timer = Substitute.For<IDispatcherTimer>();
        IDispatcher dispatcher = Substitute.For<IDispatcher>();
        dispatcher.CreateTimer().Returns(timer);
        return (dispatcher, timer);
    }

    #region Loading
    [Fact]
    public void ClipId_Set_LoadsTheClipSelectsAllOfItAndLoadsItIntoThePlayer()
    {
        AudioClip clip = TestData.Clip("Song", 42);
        _library.GetClipsAsync().Returns(TestData.Clips(clip));
        _editor.GetWaveformPeaksAsync(clip.FilePath, 400).Returns([0.2f, 0.8f]);
        TrimViewModel viewModel = Create();

        viewModel.ClipId = clip.Id;

        Assert.Same(clip, viewModel.LoadedClip);
        Assert.Equal(42, viewModel.DurationSeconds);
        Assert.Equal(0, viewModel.TrimStartSeconds);
        Assert.Equal(42, viewModel.TrimEndSeconds);
        Assert.Equal(1, viewModel.ZoomLevel);
        Assert.Equal([0.2f, 0.8f], viewModel.WaveformPeaks);
        _playback.Received(1).LoadAsync(clip);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public void ClipId_VeryShortClip_HasAtLeastATenthOfASecondToSelect()
    {
        TrimViewModel viewModel = Loaded(seconds: 0.01);

        Assert.Equal(0.1, viewModel.DurationSeconds);
        Assert.Equal(0.1, viewModel.TrimEndSeconds);
    }

    [Fact]
    public void ClipId_UnknownClip_SaysSo()
    {
        _library.GetClipsAsync().Returns(TestData.Clips());
        TrimViewModel viewModel = Create();

        viewModel.ClipId = "missing";

        Assert.Null(viewModel.LoadedClip);
        Assert.Equal("Could not find that clip.", viewModel.StatusMessage);
    }

    [Fact]
    public void ClipId_ClipCantBeRead_SaysSo()
    {
        AudioClip clip = TestData.Clip("Broken");
        _library.GetClipsAsync().Returns(TestData.Clips(clip));
        _editor.GetWaveformPeaksAsync(clip.FilePath, 400).Returns<float[]>(_ => throw new IOException());
        TrimViewModel viewModel = Create();

        viewModel.ClipId = clip.Id;

        Assert.Equal("Couldn't load that clip.", viewModel.StatusMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public void ClipId_LoadingAnotherClip_ForgetsTheOldSelectionHistory()
    {
        TrimViewModel viewModel = Loaded();
        viewModel.BeginHandleDrag();
        Assert.True(viewModel.UndoCommand.CanExecute(null));

        AudioClip next = TestData.Clip("Next", 30);
        _library.GetClipsAsync().Returns(TestData.Clips(next));
        viewModel.ClipId = next.Id;

        Assert.False(viewModel.UndoCommand.CanExecute(null));
        Assert.False(viewModel.RedoCommand.CanExecute(null));
    }
    #endregion

    #region Choosing the range
    [Fact]
    public void Nudges_MoveTheHandlesByATenthOfASecond()
    {
        TrimViewModel viewModel = Loaded(seconds: 10);
        viewModel.TrimStartSeconds = 2;
        viewModel.TrimEndSeconds = 8;

        viewModel.IncreaseStartCommand.Execute(null);
        viewModel.DecreaseEndCommand.Execute(null);

        Assert.Equal(2.1, viewModel.TrimStartSeconds, 6);
        Assert.Equal(7.9, viewModel.TrimEndSeconds, 6);

        viewModel.DecreaseStartCommand.Execute(null);
        viewModel.IncreaseEndCommand.Execute(null);

        Assert.Equal(2.0, viewModel.TrimStartSeconds, 6);
        Assert.Equal(8.0, viewModel.TrimEndSeconds, 6);
    }

    [Fact]
    public void Nudges_StopAtTheEdgesOfTheClipAndOfEachOther()
    {
        TrimViewModel viewModel = Loaded(seconds: 10);

        viewModel.DecreaseStartCommand.Execute(null);
        viewModel.IncreaseEndCommand.Execute(null);
        Assert.Equal(0, viewModel.TrimStartSeconds);
        Assert.Equal(10, viewModel.TrimEndSeconds);

        viewModel.TrimStartSeconds = 5;
        viewModel.TrimEndSeconds = 5.05;
        viewModel.IncreaseStartCommand.Execute(null);
        Assert.Equal(5.05, viewModel.TrimStartSeconds, 6);
        viewModel.DecreaseEndCommand.Execute(null);
        Assert.Equal(5.05, viewModel.TrimEndSeconds, 6);
    }

    [Fact]
    public void SetStartTime_IsClampedBetweenTheStartOfTheClipAndTheEndHandle()
    {
        TrimViewModel viewModel = Loaded(seconds: 10);
        viewModel.TrimEndSeconds = 6;

        viewModel.SetStartTime(-3);
        Assert.Equal(0, viewModel.TrimStartSeconds);

        viewModel.SetStartTime(4);
        Assert.Equal(4, viewModel.TrimStartSeconds);

        viewModel.SetStartTime(9);
        Assert.Equal(6, viewModel.TrimStartSeconds);
    }

    [Fact]
    public void SetEndTime_IsClampedBetweenTheStartHandleAndTheEndOfTheClip()
    {
        TrimViewModel viewModel = Loaded(seconds: 10);
        viewModel.TrimStartSeconds = 4;

        viewModel.SetEndTime(99);
        Assert.Equal(10, viewModel.TrimEndSeconds);

        viewModel.SetEndTime(7);
        Assert.Equal(7, viewModel.TrimEndSeconds);

        viewModel.SetEndTime(1);
        Assert.Equal(4, viewModel.TrimEndSeconds);
    }

    [Fact]
    public void TimeTexts_ShowMinutesSecondsAndTenths()
    {
        TrimViewModel viewModel = Loaded(seconds: 125.5);
        viewModel.TrimStartSeconds = 65.5;

        Assert.Equal("01:05.5", viewModel.StartTimeText);
        Assert.Equal("02:05.5", viewModel.EndTimeText);
        Assert.Equal("02:05.5", viewModel.TotalTimeText);
    }

    [Fact]
    public void SetMode_ChoosesBetweenKeepingAndRemovingTheSelection()
    {
        TrimViewModel viewModel = Loaded();
        Assert.Equal(TrimMode.Trim, viewModel.Mode);

        viewModel.SetModeCommand.Execute(TrimMode.TrimMiddle);

        Assert.Equal(TrimMode.TrimMiddle, viewModel.Mode);
    }

    [Fact]
    public void ToggleTool_RevealsATool_AndTappingItAgainHidesIt()
    {
        TrimViewModel viewModel = Loaded();

        viewModel.ToggleToolCommand.Execute(TrimTool.FadeIn);
        Assert.Equal(TrimTool.FadeIn, viewModel.ActiveTool);

        viewModel.ToggleToolCommand.Execute(TrimTool.Volume);
        Assert.Equal(TrimTool.Volume, viewModel.ActiveTool);

        viewModel.ToggleToolCommand.Execute(TrimTool.Volume);
        Assert.Equal(TrimTool.None, viewModel.ActiveTool);
    }
    #endregion

    #region Undo and redo
    [Fact]
    public void Undo_PutsBackTheSelectionFromBeforeTheDrag()
    {
        TrimViewModel viewModel = Loaded(seconds: 10);
        viewModel.BeginHandleDrag();
        viewModel.TrimStartSeconds = 3;
        viewModel.TrimEndSeconds = 6;

        viewModel.UndoCommand.Execute(null);

        Assert.Equal(0, viewModel.TrimStartSeconds);
        Assert.Equal(10, viewModel.TrimEndSeconds);
    }

    [Fact]
    public void Redo_ReappliesWhatWasUndone()
    {
        TrimViewModel viewModel = Loaded(seconds: 10);
        viewModel.BeginHandleDrag();
        viewModel.TrimStartSeconds = 3;
        viewModel.TrimEndSeconds = 6;
        viewModel.UndoCommand.Execute(null);

        viewModel.RedoCommand.Execute(null);

        Assert.Equal(3, viewModel.TrimStartSeconds);
        Assert.Equal(6, viewModel.TrimEndSeconds);
    }

    [Fact]
    public void UndoAndRedo_AreOnlyAvailableWhenThereIsSomethingToDo()
    {
        TrimViewModel viewModel = Loaded(seconds: 10);
        Assert.False(viewModel.UndoCommand.CanExecute(null));
        Assert.False(viewModel.RedoCommand.CanExecute(null));

        viewModel.BeginHandleDrag();
        Assert.True(viewModel.UndoCommand.CanExecute(null));
        Assert.False(viewModel.RedoCommand.CanExecute(null));

        viewModel.UndoCommand.Execute(null);
        Assert.False(viewModel.UndoCommand.CanExecute(null));
        Assert.True(viewModel.RedoCommand.CanExecute(null));
    }

    [Fact]
    public void BeginHandleDrag_AfterAnUndo_ClearsWhatCouldHaveBeenRedone()
    {
        TrimViewModel viewModel = Loaded(seconds: 10);
        viewModel.BeginHandleDrag();
        viewModel.TrimStartSeconds = 3;
        viewModel.UndoCommand.Execute(null);
        Assert.True(viewModel.RedoCommand.CanExecute(null));

        viewModel.BeginHandleDrag();

        Assert.False(viewModel.RedoCommand.CanExecute(null));
    }
    #endregion

    #region Zoom and the ruler
    [Fact]
    public void ZoomIn_StepsThroughTheZoomLevelsAndStopsAtTheLast()
    {
        TrimViewModel viewModel = Loaded();
        List<double> seen = [];

        for (int i = 0; i < 7; i++)
        {
            viewModel.ZoomInCommand.Execute(null);
            seen.Add(viewModel.ZoomLevel);
        }

        Assert.Equal([2, 4, 8, 16, 16, 16, 16], seen);
    }

    [Fact]
    public void ZoomOut_StepsBackDownAndStopsAtWholeClip()
    {
        TrimViewModel viewModel = Loaded();
        viewModel.ZoomLevel = 8;
        List<double> seen = [];

        for (int i = 0; i < 5; i++)
        {
            viewModel.ZoomOutCommand.Execute(null);
            seen.Add(viewModel.ZoomLevel);
        }

        Assert.Equal([4, 2, 1, 1, 1], seen);
    }

    [Fact]
    public void Zoom_ShowsAShorterStretchOfTheClipAndScalesThePixelsPerSecond()
    {
        TrimViewModel viewModel = Loaded(seconds: 100);
        viewModel.ViewportWidth = 400;

        Assert.False(viewModel.IsZoomed);
        Assert.Equal(100, viewModel.VisibleSeconds);
        Assert.Equal(4, viewModel.PixelsPerSecond);
        Assert.Equal("1x", viewModel.ZoomLevelText);

        viewModel.ZoomInCommand.Execute(null);

        Assert.True(viewModel.IsZoomed);
        Assert.Equal(50, viewModel.VisibleSeconds);
        Assert.Equal(8, viewModel.PixelsPerSecond);
        Assert.Equal("2x", viewModel.ZoomLevelText);
    }

    [Fact]
    public void HandlePositions_AreRelativeToTheZoomWindow()
    {
        TrimViewModel viewModel = Loaded(seconds: 100);
        viewModel.ViewportWidth = 400;
        viewModel.TrimStartSeconds = 10;
        viewModel.TrimEndSeconds = 90;

        Assert.Equal(40, viewModel.StartHandleX);
        Assert.Equal(360, viewModel.EndHandleX);
    }

    [Theory]
    [InlineData(5, 1)]
    [InlineData(10, 1)]
    [InlineData(60, 10)]
    [InlineData(100, 10)]
    [InlineData(600, 60)]
    [InlineData(100_000, 1800)]
    public void RulerStep_IsTheSmallestNiceStepGivingAtMostTenTicks(double clipSeconds, double expectedStep)
    {
        TrimViewModel viewModel = Loaded(seconds: clipSeconds);

        Assert.Equal(expectedStep, viewModel.RulerStepSeconds);
    }

    [Fact]
    public void PanLaterAndEarlier_MoveTheWindowByHalfItsWidthWithinTheClip()
    {
        TrimViewModel viewModel = Loaded(seconds: 100);
        viewModel.ZoomLevel = 4; // 25 s visible

        viewModel.PanLaterCommand.Execute(null);
        Assert.Equal(12.5, viewModel.WindowStartSeconds);

        for (int i = 0; i < 10; i++)
        {
            viewModel.PanLaterCommand.Execute(null);
        }

        Assert.Equal(75, viewModel.WindowStartSeconds); // the last window ends at the end of the clip

        for (int i = 0; i < 10; i++)
        {
            viewModel.PanEarlierCommand.Execute(null);
        }

        Assert.Equal(0, viewModel.WindowStartSeconds);
    }

    [Fact]
    public void MovingAHandleTowardTheEdgeOfAZoomedWindow_ScrollsTheWindowAfterIt()
    {
        TrimViewModel viewModel = Loaded(seconds: 100);
        viewModel.ZoomLevel = 4; // 25 s visible, 2.5 s margin

        viewModel.SetEndTime(50);

        Assert.Equal(27.5, viewModel.WindowStartSeconds);
    }

    [Fact]
    public void MovingAHandleWhileNotZoomed_LeavesTheWindowAlone()
    {
        TrimViewModel viewModel = Loaded(seconds: 100);

        viewModel.SetEndTime(50);

        Assert.Equal(0, viewModel.WindowStartSeconds);
    }
    #endregion

    #region Saving
    [Fact]
    public async Task Save_TrimMode_KeepsTheSelectionSavesItAndGoesBack()
    {
        TrimViewModel viewModel = Loaded(seconds: 10, name: "Song");
        AudioClip clip = viewModel.LoadedClip!;
        viewModel.TrimStartSeconds = 2;
        viewModel.TrimEndSeconds = 6;
        _editor.TrimAsync(clip.FilePath, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(6), "Song (trimmed)").Returns("trimmed.wav");
        _library.AddClipAsync("trimmed.wav", "Song (trimmed)", true).Returns(TestData.Clip("Song (trimmed)"));

        await viewModel.SaveCommand.ExecuteAsync(null);

        await _editor.DidNotReceiveWithAnyArgs().CutAsync(default!, default, default, default!);
        await _editor.DidNotReceiveWithAnyArgs().ApplyFadeAsync(default!, default, default, default!);
        await _editor.DidNotReceiveWithAnyArgs().ApplyGainAsync(default!, default, default!);
        await _library.Received(1).AddClipAsync("trimmed.wav", "Song (trimmed)", true);
        await _navigation.Received(1).GoToAsync("..");
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task Save_TrimMiddleMode_RemovesTheSelectionInstead()
    {
        TrimViewModel viewModel = Loaded(seconds: 10, name: "Song");
        AudioClip clip = viewModel.LoadedClip!;
        viewModel.Mode = TrimMode.TrimMiddle;
        viewModel.TrimStartSeconds = 2;
        viewModel.TrimEndSeconds = 6;
        _editor.CutAsync(clip.FilePath, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(6), "Song (trimmed)").Returns("cut.wav");
        _library.AddClipAsync("cut.wav", "Song (trimmed)", true).Returns(TestData.Clip("Song (trimmed)"));

        await viewModel.SaveCommand.ExecuteAsync(null);

        await _editor.DidNotReceiveWithAnyArgs().TrimAsync(default!, default, default, default!);
        await _library.Received(1).AddClipAsync("cut.wav", "Song (trimmed)", true);
    }

    [Fact]
    public async Task Save_WithFadeAndGain_AppliesThemToTheTrimmedFileInThatOrder()
    {
        TrimViewModel viewModel = Loaded(seconds: 10, name: "Song");
        AudioClip clip = viewModel.LoadedClip!;
        viewModel.FadeInSeconds = 0.5;
        viewModel.FadeOutSeconds = 1;
        viewModel.GainDb = 3;
        _editor.TrimAsync(clip.FilePath, TimeSpan.Zero, TimeSpan.FromSeconds(10), "Song (trimmed)").Returns("trimmed.wav");
        _editor.ApplyFadeAsync("trimmed.wav", TimeSpan.FromSeconds(0.5), TimeSpan.FromSeconds(1), "Song (trimmed)").Returns("faded.wav");
        _editor.ApplyGainAsync("faded.wav", 3, "Song (trimmed)").Returns("gained.wav");
        _library.AddClipAsync("gained.wav", "Song (trimmed)", true).Returns(TestData.Clip("Song (trimmed)"));

        await viewModel.SaveCommand.ExecuteAsync(null);

        await _library.Received(1).AddClipAsync("gained.wav", "Song (trimmed)", true);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.01)]
    [InlineData(-0.01)]
    public async Task Save_GainTooSmallToHear_IsNotApplied(double gainDb)
    {
        TrimViewModel viewModel = Loaded(seconds: 10);
        viewModel.GainDb = gainDb;
        _editor.TrimAsync(default!, default, default, default!).ReturnsForAnyArgs("trimmed.wav");
        _library.AddClipAsync(default!, default!, default).ReturnsForAnyArgs(TestData.Clip());

        await viewModel.SaveCommand.ExecuteAsync(null);

        await _editor.DidNotReceiveWithAnyArgs().ApplyGainAsync(default!, default, default!);
    }

    [Fact]
    public async Task Save_OnlyAFadeOut_StillAppliesTheFade()
    {
        TrimViewModel viewModel = Loaded(seconds: 10);
        viewModel.FadeOutSeconds = 2;
        _editor.TrimAsync(default!, default, default, default!).ReturnsForAnyArgs("trimmed.wav");
        _editor.ApplyFadeAsync(default!, default, default, default!).ReturnsForAnyArgs("faded.wav");
        _library.AddClipAsync(default!, default!, default).ReturnsForAnyArgs(TestData.Clip());

        await viewModel.SaveCommand.ExecuteAsync(null);

        await _editor.Received(1).ApplyFadeAsync("trimmed.wav", TimeSpan.Zero, TimeSpan.FromSeconds(2), Arg.Any<string>());
    }

    [Fact]
    public async Task Save_EditFails_ReportsItAndStaysOnThePage()
    {
        TrimViewModel viewModel = Loaded(seconds: 10);
        _editor.TrimAsync(default!, default, default, default!).ReturnsForAnyArgs(Task.FromException<string>(new IOException()));

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Couldn't save the trimmed clip.", viewModel.StatusMessage);
        Assert.False(viewModel.IsBusy);
        await _navigation.DidNotReceiveWithAnyArgs().GoToAsync(default!);
    }

    [Fact]
    public async Task Save_NoClipLoaded_DoesNothing()
    {
        await Create().SaveCommand.ExecuteAsync(null);

        Assert.Empty(_editor.ReceivedCalls());
        await _navigation.DidNotReceiveWithAnyArgs().GoToAsync(default!);
    }
    #endregion

    #region Splitting and inserting
    [Fact]
    public async Task SplitAtPlayhead_CutsTheClipInTwoAddsBothPartsAndGoesBack()
    {
        TrimViewModel viewModel = Loaded(seconds: 10, name: "Song");
        AudioClip clip = viewModel.LoadedClip!;
        viewModel.PositionSeconds = 4;
        _editor.SplitAsync(clip.FilePath, TimeSpan.FromSeconds(4), "Song (part 1)", "Song (part 2)").Returns(("one.wav", "two.wav"));

        await viewModel.SplitAtPlayheadCommand.ExecuteAsync(null);

        await _library.Received(1).AddClipAsync("one.wav", "Song (part 1)", true);
        await _library.Received(1).AddClipAsync("two.wav", "Song (part 2)", true);
        await _navigation.Received(1).GoToAsync("..");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10)]
    [InlineData(11)]
    public async Task SplitAtPlayhead_PlayheadOutsideTheClip_AsksForABetterPosition(double position)
    {
        TrimViewModel viewModel = Loaded(seconds: 10);
        viewModel.PositionSeconds = position;

        await viewModel.SplitAtPlayheadCommand.ExecuteAsync(null);

        Assert.Equal("Move the playhead into the clip first.", viewModel.StatusMessage);
        await _editor.DidNotReceiveWithAnyArgs().SplitAsync(default!, default, default!, default!);
    }

    [Fact]
    public async Task SplitAtPlayhead_NoClipLoaded_AsksForABetterPosition()
    {
        TrimViewModel viewModel = Create();

        await viewModel.SplitAtPlayheadCommand.ExecuteAsync(null);

        Assert.Equal("Move the playhead into the clip first.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task SplitAtPlayhead_SplitFails_ReportsItAndStaysOnThePage()
    {
        TrimViewModel viewModel = Loaded(seconds: 10);
        viewModel.PositionSeconds = 4;
        _editor.SplitAsync(default!, default, default!, default!).ReturnsForAnyArgs(Task.FromException<(string, string)>(new InvalidDataException()));

        await viewModel.SplitAtPlayheadCommand.ExecuteAsync(null);

        Assert.Equal("Couldn't split that clip.", viewModel.StatusMessage);
        await _navigation.DidNotReceiveWithAnyArgs().GoToAsync(default!);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task InsertClip_PutsTheOtherClipInAtThePlayheadAndGoesBack()
    {
        TrimViewModel viewModel = Loaded(seconds: 10, name: "Song");
        AudioClip clip = viewModel.LoadedClip!;
        AudioClip other = TestData.Clip("Other");
        viewModel.PositionSeconds = 3;
        _editor.InsertAsync(clip.FilePath, other.FilePath, TimeSpan.FromSeconds(3), "Song (inserted)").Returns("inserted.wav");
        _library.AddClipAsync("inserted.wav", "Song (inserted)", true).Returns(TestData.Clip("Song (inserted)"));

        await viewModel.InsertClipCommand.ExecuteAsync(other);

        await _library.Received(1).AddClipAsync("inserted.wav", "Song (inserted)", true);
        await _navigation.Received(1).GoToAsync("..");
    }

    [Fact]
    public async Task InsertClip_NothingChosen_DoesNothing()
    {
        TrimViewModel viewModel = Loaded();

        await viewModel.InsertClipCommand.ExecuteAsync(null);

        await _editor.DidNotReceiveWithAnyArgs().InsertAsync(default!, default!, default, default!);
    }

    [Fact]
    public async Task InsertClip_InsertFails_ReportsIt()
    {
        TrimViewModel viewModel = Loaded();
        _editor.InsertAsync(default!, default!, default, default!).ReturnsForAnyArgs(Task.FromException<string>(new NotSupportedException()));

        await viewModel.InsertClipCommand.ExecuteAsync(TestData.Clip("Other"));

        Assert.Equal("Couldn't insert that clip.", viewModel.StatusMessage);
        await _navigation.DidNotReceiveWithAnyArgs().GoToAsync(default!);
    }

    [Fact]
    public async Task GetLibraryClips_ListsTheLibrariesClipsForThePicker()
    {
        AudioClip clip = TestData.Clip("Song");
        _library.GetClipsAsync().Returns(TestData.Clips(clip));

        IReadOnlyList<AudioClip> clips = await Create().GetLibraryClipsAsync();

        Assert.Equal([clip], clips);
    }
    #endregion

    #region Snapping to zero crossings
    [Fact]
    public async Task SnapEndToZeroCrossing_MovesTheEndHandleToWhereTheEditorFindsOne()
    {
        TrimViewModel viewModel = Loaded(seconds: 10);
        AudioClip clip = viewModel.LoadedClip!;
        viewModel.TrimEndSeconds = 8;
        _editor.FindNearestZeroCrossingAsync(clip.FilePath, TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(0.01)).Returns(TimeSpan.FromSeconds(8.004));

        await viewModel.SnapEndToZeroCrossingAsync();

        Assert.Equal(8.004, viewModel.TrimEndSeconds, 6);
    }

    [Fact]
    public async Task SnapStartToZeroCrossing_MovesTheStartHandleToWhereTheEditorFindsOne()
    {
        TrimViewModel viewModel = Loaded(seconds: 10);
        AudioClip clip = viewModel.LoadedClip!;
        viewModel.TrimStartSeconds = 2;
        _editor.FindNearestZeroCrossingAsync(clip.FilePath, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(0.01)).Returns(TimeSpan.FromSeconds(1.996));

        await viewModel.SnapStartToZeroCrossingAsync();

        Assert.Equal(1.996, viewModel.TrimStartSeconds, 6);
    }

    [Fact]
    public async Task Snap_ResultOutsideTheSelection_IsClampedToIt()
    {
        TrimViewModel viewModel = Loaded(seconds: 10);
        viewModel.TrimStartSeconds = 4;
        viewModel.TrimEndSeconds = 6;
        _editor.FindNearestZeroCrossingAsync(default!, default, default).ReturnsForAnyArgs(TimeSpan.FromSeconds(99));

        await viewModel.SnapEndToZeroCrossingAsync();
        Assert.Equal(10, viewModel.TrimEndSeconds);

        _editor.FindNearestZeroCrossingAsync(default!, default, default).ReturnsForAnyArgs(TimeSpan.FromSeconds(-5));
        await viewModel.SnapStartToZeroCrossingAsync();
        Assert.Equal(0, viewModel.TrimStartSeconds);
    }

    [Fact]
    public async Task Snap_EditorFails_LeavesTheHandlesWhereTheyWere()
    {
        TrimViewModel viewModel = Loaded(seconds: 10);
        viewModel.TrimStartSeconds = 2;
        viewModel.TrimEndSeconds = 8;
        _editor.FindNearestZeroCrossingAsync(default!, default, default).ReturnsForAnyArgs(Task.FromException<TimeSpan>(new IOException()));

        await viewModel.SnapStartToZeroCrossingAsync();
        await viewModel.SnapEndToZeroCrossingAsync();

        Assert.Equal(2, viewModel.TrimStartSeconds);
        Assert.Equal(8, viewModel.TrimEndSeconds);
    }

    [Fact]
    public async Task Snap_NoClipLoaded_DoesNothing()
    {
        TrimViewModel viewModel = Create();

        await viewModel.SnapStartToZeroCrossingAsync();
        await viewModel.SnapEndToZeroCrossingAsync();

        await _editor.DidNotReceiveWithAnyArgs().FindNearestZeroCrossingAsync(default!, default, default);
    }
    #endregion

    #region Playback
    [Fact]
    public void PlayPause_TogglesTheSharedTransport()
    {
        TrimViewModel viewModel = Loaded();

        _playback.IsPlaying.Returns(false);
        viewModel.PlayPauseCommand.Execute(null);
        _playback.IsPlaying.Returns(true);
        viewModel.PlayPauseCommand.Execute(null);

        Received.InOrder(() =>
        {
            _playback.Play();
            _playback.Pause();
        });
    }

    [Fact]
    public void SkipToStartAndEnd_SeekToTheSelectionsHandles()
    {
        TrimViewModel viewModel = Loaded(seconds: 10);
        viewModel.TrimStartSeconds = 2;
        viewModel.TrimEndSeconds = 8;

        viewModel.SkipToStartCommand.Execute(null);
        viewModel.SkipToEndCommand.Execute(null);

        Received.InOrder(() =>
        {
            _playback.Seek(TimeSpan.FromSeconds(2));
            _playback.Seek(TimeSpan.FromSeconds(8));
        });
    }

    [Theory]
    [InlineData(-4, 0)]
    [InlineData(3.5, 3.5)]
    [InlineData(50, 10)]
    public void SeekToPosition_IsClampedToTheClipAndMovesThePlayhead(double requested, double expected)
    {
        TrimViewModel viewModel = Loaded(seconds: 10);

        viewModel.SeekToPosition(requested);

        _playback.Received(1).Seek(TimeSpan.FromSeconds(expected));
        Assert.Equal(expected, viewModel.PositionSeconds);
    }

    [Fact]
    public void StartTicking_FollowsThePlaybackPositionAndState()
    {
        (IDispatcher dispatcher, IDispatcherTimer timer) = CreateDispatcher();
        TrimViewModel viewModel = Loaded();

        viewModel.StartTicking(dispatcher);
        _playback.Position.Returns(TimeSpan.FromSeconds(9));
        _playback.IsPlaying.Returns(true);
        timer.Tick += Raise.Event();

        Assert.Equal(TimeSpan.FromMilliseconds(100), timer.Interval);
        timer.Received(1).Start();
        Assert.Equal(9, viewModel.PositionSeconds);
        Assert.True(viewModel.IsPlaying);
    }

    [Fact]
    public void StartTicking_CalledTwice_StillUsesOneTimer()
    {
        (IDispatcher dispatcher, _) = CreateDispatcher();
        TrimViewModel viewModel = Loaded();

        viewModel.StartTicking(dispatcher);
        viewModel.StartTicking(dispatcher);

        dispatcher.Received(1).CreateTimer();
    }

    [Fact]
    public void StopTicking_StopsTheTimer_AndIsHarmlessIfNeverStarted()
    {
        (IDispatcher dispatcher, IDispatcherTimer timer) = CreateDispatcher();
        TrimViewModel viewModel = Loaded();
        viewModel.StopTicking();

        viewModel.StartTicking(dispatcher);
        viewModel.StopTicking();

        timer.Received(1).Stop();
    }
    #endregion
}
