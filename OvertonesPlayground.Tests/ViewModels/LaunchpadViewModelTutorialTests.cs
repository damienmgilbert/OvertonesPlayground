using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Tests.ViewModels;

///<summary>
///Runs the tutorials on the real Launchpad view model, with the real setups built from the real sound bank (the audio itself is
///a substitute). What matters here is that a tutorial borrows the pads without harming the user's own, that each step does what
///it says, and that every tutorial can be played to its last step.
///</summary>
public sealed class LaunchpadViewModelTutorialTests : IDisposable
{
    #region Fields
    private readonly ISampleAssetStore _assets = Substitute.For<ISampleAssetStore>();
    private readonly ISampleCatalogService _catalog = Substitute.For<ISampleCatalogService>();
    private readonly TempFileSystem _files = new();
    private readonly IAudioLibraryService _library = Substitute.For<IAudioLibraryService>();
    private readonly IMixdownService _mixdown = Substitute.For<IMixdownService>();
    private readonly IAudioPlaybackService _playback = Substitute.For<IAudioPlaybackService>();
    private readonly FakePreferences _preferences = new();
    private readonly ISoundSynthesisService _synthesis = Substitute.For<ISoundSynthesisService>();
    #endregion

    #region Constructors
    public LaunchpadViewModelTutorialTests()
    {
        _catalog.GetIndexAsync(Arg.Any<CancellationToken>()).Returns(RealCatalog.Index);
        _assets.CopyToAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
        call =>
        {
            string directory = call.ArgAt<string>(1);
            _ = Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, call.ArgAt<string>(0));
            File.WriteAllBytes(path, [1, 2, 3]);
            return Task.FromResult(path);
        });
        _synthesis.GenerateToneAsync(Arg.Any<WaveformType>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<string>())
            .Returns(call => TestData.Clip(call.ArgAt<string>(4), path: _files.CreateFile($"click/{call.ArgAt<string>(4)}.wav")));
    }
    #endregion

    #region Private methods
    ///<summary>
    ///Gives pad <paramref name="index"/> a sample the user chose, with a real file behind it.
    ///</summary>
    private void GiveUserAPad(LaunchpadViewModel viewModel, int index, string name)
    {
        viewModel.AssignClip(viewModel.Pads[index], TestData.Clip(name, path: _files.CreateFile($"clips/{name}.wav")));
    }

    private LaunchpadViewModel Create() => Create(new LaunchpadExampleService(_catalog, _assets, _files));

    private LaunchpadViewModel Create(ILaunchpadExampleService examples) =>
        new(_playback, _library, _synthesis, _mixdown, _preferences, _files, examples, NullLogger<LaunchpadViewModel>.Instance)
        {
            Delay = (_, _) => Task.CompletedTask,
        };

    private static ILaunchpadTutorialHost Host(LaunchpadViewModel viewModel) => viewModel;

    private static LaunchpadTutorial Tutorial(string id) => LaunchpadTutorials.All.Single(tutorial => tutorial.Id == id);

    public static TheoryData<string> TutorialIds()
    {
        TheoryData<string> ids = [];
        foreach (LaunchpadTutorial tutorial in LaunchpadTutorials.All)
        {
            ids.Add(tutorial.Id);
        }

        return ids;
    }

    ///<summary>
    ///Everything about the pads the user would notice: what is on each, the bank and mode showing, and the saved layout.
    ///</summary>
    private string Snapshot(LaunchpadViewModel viewModel)
    {
        string pads = string.Join('|', viewModel.Pads.Select(pad => $"{pad.Index}:{pad.Label}:{pad.IsLooping}"));
        string saved = File.Exists(_files.InAppData("launchpad.json")) ? File.ReadAllText(_files.InAppData("launchpad.json")) : string.Empty;
        return $"{pads}#{viewModel.Bank}#{viewModel.Mode}#{viewModel.SelectedTrack}#{saved}";
    }

    private int VoicesStarted() => _playback.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IAudioPlaybackService.TriggerVoice));

    private int VoicesStoppedByPad() => _playback.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IAudioPlaybackService.StopPad));

    public void Dispose() => _files.Dispose();
    #endregion

    #region Public methods
    [Fact]
    public async Task StartTutorial_LoadsItsSetupAndShowsTheFirstTip()
    {
        LaunchpadViewModel viewModel = Create();
        List<LaunchpadTutorialPrompt?> prompts = [];
        viewModel.TutorialPromptChanged += (_, prompt) => prompts.Add(prompt);

        await viewModel.StartTutorialAsync(Tutorial("pads"));

        Assert.True(viewModel.IsTutorialActive);
        LaunchpadTutorialPrompt prompt = Assert.Single(prompts)!;
        Assert.Equal("Sixty-four pads (1/10)", prompt.Title);
        Assert.Equal("Next", prompt.ActionText);
        Assert.Equal("Exit", prompt.CloseText);
        Assert.True(viewModel.Pads[56].HasClip, "The kit should be on the pads.");
        Assert.Equal(LaunchpadMode.Session, viewModel.Mode);
    }

    [Fact]
    public async Task StartTutorial_OutlinesWhatTheFirstStepPointsAt()
    {
        LaunchpadViewModel viewModel = Create();

        await viewModel.StartTutorialAsync(Tutorial("pads"));

        Assert.Equal(Enumerable.Range(56, 8), viewModel.Pads.Where(pad => pad.IsSpotlit).Select(pad => pad.Index));
        Assert.Equal([LaunchpadControl.Session], viewModel.AllKeys.Where(key => key.IsSpotlit).Select(key => key.Control));
    }

    [Fact]
    public async Task ContinueTutorial_ShowMe_StepsAsideWhileItPlaysThenShowsWhatToNotice()
    {
        LaunchpadViewModel viewModel = Create();
        List<LaunchpadTutorialPrompt?> prompts = [];
        await viewModel.StartTutorialAsync(Tutorial("pads"));
        viewModel.TutorialPromptChanged += (_, prompt) => prompts.Add(prompt);
        await viewModel.ContinueTutorialAsync();
        Assert.Equal("Show me", prompts[^1]!.ActionText);
        prompts.Clear();
        int before = VoicesStarted();

        await viewModel.ContinueTutorialAsync();

        Assert.Null(prompts[0]);
        LaunchpadTutorialPrompt result = prompts[1]!;
        Assert.Equal("Tap to play (2/10)", result.Title);
        Assert.StartsWith("Kick, hat, snare, hat", result.Message, StringComparison.Ordinal);
        Assert.Equal("Next", result.ActionText);
        Assert.Equal(8, VoicesStarted() - before);
    }

    [Fact]
    public async Task ContinueTutorial_AStepWithNoResult_GoesStraightOn()
    {
        LaunchpadViewModel viewModel = Create();
        List<LaunchpadTutorialPrompt?> prompts = [];
        await viewModel.StartTutorialAsync(Tutorial("pads"));
        await viewModel.ContinueTutorialAsync();
        await viewModel.ContinueTutorialAsync();
        await viewModel.ContinueTutorialAsync();
        viewModel.TutorialPromptChanged += (_, prompt) => prompts.Add(prompt);

        // Step 3 has an action but no result: showing it plays the kicks and then moves on to step 4.
        await viewModel.ContinueTutorialAsync();

        Assert.Null(prompts[0]);
        Assert.Equal("Banks (4/10)", prompts[1]!.Title);
    }

    [Fact]
    public async Task ContinueTutorial_TheLastStep_FinishesAndGivesBackTheUsersPads()
    {
        LaunchpadViewModel viewModel = Create();
        GiveUserAPad(viewModel, 3, "Mine");
        await viewModel.StartTutorialAsync(Tutorial("timbre"));
        List<LaunchpadTutorialPrompt?> prompts = [];
        viewModel.TutorialPromptChanged += (_, prompt) => prompts.Add(prompt);

        for (int guard = 0; viewModel.IsTutorialActive && guard < 40; guard++)
        {
            await viewModel.ContinueTutorialAsync();
        }

        Assert.False(viewModel.IsTutorialActive);
        Assert.Null(prompts[^1]);
        Assert.Equal("Mine", viewModel.Pads[3].Label);
        Assert.Contains("Finished", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(TutorialIds))]
    public async Task EveryTutorial_CanBePlayedToItsLastStepAndLeavesTheUsersPadsAsTheyWere(string id)
    {
        LaunchpadViewModel viewModel = Create();
        GiveUserAPad(viewModel, 3, "Mine");
        GiveUserAPad(viewModel, 40, "Also mine");
        string before = Snapshot(viewModel);
        LaunchpadTutorial tutorial = Tutorial(id);
        List<LaunchpadTutorialPrompt?> prompts = [];
        viewModel.TutorialPromptChanged += (_, prompt) => prompts.Add(prompt);

        await viewModel.StartTutorialAsync(tutorial);
        int continues = 0;
        while (viewModel.IsTutorialActive && continues < (tutorial.Steps.Count * 2) + 2)
        {
            await viewModel.ContinueTutorialAsync();
            continues++;
        }

        Assert.False(viewModel.IsTutorialActive, "The tutorial should have reached its last step.");
        Assert.All(tutorial.Steps, step => Assert.Contains(prompts, prompt => prompt is not null && prompt.Title.StartsWith(step.Title, StringComparison.Ordinal)));
        Assert.Equal(before, Snapshot(viewModel));
        Assert.Empty(viewModel.Pads.Where(pad => pad.IsSpotlit));
        Assert.Empty(viewModel.AllKeys.Where(key => key.IsSpotlit));
        Assert.Equal(LaunchpadLayer.None, viewModel.Layer);
        Assert.False(viewModel.IsEditMode);
        Assert.False(viewModel.IsPlaying);
    }

    [Theory]
    [MemberData(nameof(TutorialIds))]
    public async Task EveryTutorial_MakesSound(string id)
    {
        LaunchpadViewModel viewModel = Create();
        LaunchpadTutorial tutorial = Tutorial(id);
        int before = VoicesStarted();

        await viewModel.StartTutorialAsync(tutorial);
        for (int continues = 0; viewModel.IsTutorialActive && continues < (tutorial.Steps.Count * 2) + 2; continues++)
        {
            await viewModel.ContinueTutorialAsync();
        }

        Assert.True(VoicesStarted() - before >= 3, $"'{id}' should play the sounds it is about; it started {VoicesStarted() - before}.");
    }

    [Fact]
    public async Task Tutorial_NeverSavesOverTheUsersLayout()
    {
        LaunchpadViewModel viewModel = Create();
        GiveUserAPad(viewModel, 3, "Mine");
        string saved = File.ReadAllText(_files.InAppData("launchpad.json"));

        await viewModel.StartTutorialAsync(Tutorial("rhythm"));
        for (int continues = 0; continues < 10 && viewModel.IsTutorialActive; continues++)
        {
            await viewModel.ContinueTutorialAsync();
            Assert.Equal(saved, File.ReadAllText(_files.InAppData("launchpad.json")));
        }

        Assert.True(viewModel.IsTutorialActive);
        viewModel.ExitTutorial();
        Assert.Equal("Mine", viewModel.Pads[3].Label);
    }

    [Fact]
    public async Task ExitTutorial_MidWay_PutsBackThePadsAndTheMode()
    {
        LaunchpadViewModel viewModel = Create();
        GiveUserAPad(viewModel, 3, "Mine");
        viewModel.PressKey(viewModel.AllKeys.First(key => key.Control == LaunchpadControl.Note));
        LaunchpadMode mode = viewModel.Mode;

        await viewModel.StartTutorialAsync(Tutorial("timing"));
        await viewModel.ContinueTutorialAsync();
        await viewModel.ContinueTutorialAsync();
        Assert.True(viewModel.IsPlaying, "The timing tutorial starts the beat.");
        viewModel.ExitTutorial();

        Assert.False(viewModel.IsTutorialActive);
        Assert.Equal("Mine", viewModel.Pads[3].Label);
        Assert.Equal(LaunchpadMode.Note, mode);
        Assert.Equal(mode, viewModel.Mode);
        Assert.False(viewModel.IsPlaying);
    }

    [Fact]
    public async Task ExitTutorial_WhileAStepIsPlaying_StopsItWithoutError()
    {
        LaunchpadViewModel viewModel = Create();
        GiveUserAPad(viewModel, 3, "Mine");
        viewModel.Delay = (_, token) => Task.Delay(Timeout.Infinite, token);
        await viewModel.StartTutorialAsync(Tutorial("pads"));
        await viewModel.ContinueTutorialAsync();

        Task playing = viewModel.ContinueTutorialAsync();
        Assert.False(playing.IsCompleted, "The step should be waiting between taps.");
        viewModel.ExitTutorial();
        await playing;

        Assert.False(viewModel.IsTutorialActive);
        Assert.Equal("Mine", viewModel.Pads[3].Label);
    }

    [Fact]
    public async Task StartTutorial_TheSetupCantBeBuilt_LeavesThePadsAlone()
    {
        ILaunchpadExampleService examples = Substitute.For<ILaunchpadExampleService>();
        examples.CreateLessonAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<LaunchpadProject>(new InvalidDataException("no catalog")));
        LaunchpadViewModel viewModel = Create(examples);
        GiveUserAPad(viewModel, 3, "Mine");
        string before = Snapshot(viewModel);

        await viewModel.StartTutorialAsync(Tutorial("pads"));

        Assert.False(viewModel.IsTutorialActive);
        Assert.Equal(before, Snapshot(viewModel));
        Assert.Contains("Couldn't start", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartTutorial_WhileAnotherRuns_LeavesTheFirstAndStartsTheSecondFromTheUsersOwnPads()
    {
        LaunchpadViewModel viewModel = Create();
        GiveUserAPad(viewModel, 3, "Mine");
        await viewModel.StartTutorialAsync(Tutorial("pads"));

        await viewModel.StartTutorialAsync(Tutorial("melody"));
        viewModel.ExitTutorial();

        Assert.Equal("Mine", viewModel.Pads[3].Label);
        Assert.Empty(viewModel.Pads.Where(pad => pad.HasClip && pad.Index != 3));
    }

    [Fact]
    public void Tutorials_ListsAllNineForTheGuideMenu() => Assert.Equal(9, Create().Tutorials.Count);

    [Fact]
    public async Task Host_Press_WithShift_RunsTheSecondFunctionAndReleasesShift()
    {
        LaunchpadViewModel viewModel = Create();

        await Host(viewModel).PressAsync(LaunchpadControl.Device, shifted: true);

        Assert.Equal(LaunchpadLayer.Tempo, viewModel.Layer);
        Assert.False(viewModel.IsShiftLatched);
    }

    [Fact]
    public async Task Host_Press_ATrackButton_IsRefused() =>
        _ = await Assert.ThrowsAsync<ArgumentException>(() => Host(Create()).PressAsync(LaunchpadControl.Track));

    [Fact]
    public async Task Host_Tap_PlaysEachPadInOrderAndARestIsSilent()
    {
        LaunchpadViewModel viewModel = Create();
        GiveUserAPad(viewModel, 5, "Kick");
        int before = VoicesStarted();

        await Host(viewModel).TapAsync([5, -1, 5], 0);

        Assert.Equal(2, VoicesStarted() - before);
    }

    [Theory]
    [InlineData(64)]
    [InlineData(100)]
    public async Task Host_Tap_APadThatDoesNotExist_Throws(int pad) =>
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Host(Create()).TapAsync([pad], 0));

    [Fact]
    public async Task Host_Hold_InCustomMode_SoundsUntilItIsLetGo()
    {
        LaunchpadViewModel viewModel = Create();
        GiveUserAPad(viewModel, 5, "Chord");
        viewModel.PressKey(viewModel.AllKeys.First(key => key.Control == LaunchpadControl.Custom));
        int started = VoicesStarted();
        int stopped = VoicesStoppedByPad();

        await Host(viewModel).HoldAsync(5, 10);

        Assert.Equal(1, VoicesStarted() - started);
        Assert.Equal(1, VoicesStoppedByPad() - stopped);
    }

    [Fact]
    public async Task Host_ShowBank_GoesToTheBank()
    {
        LaunchpadViewModel viewModel = Create();

        await Host(viewModel).ShowBankAsync(2);
        Assert.Equal(2, viewModel.Bank);
        await Host(viewModel).ShowBankAsync(0);
        Assert.Equal(0, viewModel.Bank);
    }

    [Fact]
    public async Task Host_AssignFromSoundBank_PutsTheSoundOnThePadInItsColumnsColor()
    {
        LaunchpadViewModel viewModel = Create();

        await Host(viewModel).AssignFromSoundBankAsync(60, "Cowbell 808 DMX");

        LaunchpadPadViewModel pad = viewModel.Pads[60];
        Assert.Equal("Cowbell 808 DMX", pad.Label);
        Assert.True(File.Exists(pad.Pad.ClipPath));
        Assert.Equal(LaunchpadColumn.Colors[4], pad.Pad.ColorHex);
    }

    [Fact]
    public async Task Host_AssignFromSoundBank_ASoundThatIsNotThere_Throws() =>
        _ = await Assert.ThrowsAsync<ArgumentException>(() => Host(Create()).AssignFromSoundBankAsync(60, "No Such Sound"));

    [Fact]
    public async Task Host_SetScale_ChangesTheScaleForNoteAndChord()
    {
        LaunchpadViewModel viewModel = Create();

        await Host(viewModel).SetScaleAsync(2);

        Assert.Contains("Pentatonic", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Host_ToggleLoop_LoopsThePad()
    {
        LaunchpadViewModel viewModel = Create();
        GiveUserAPad(viewModel, 5, "Cowbell");

        await Host(viewModel).ToggleLoopAsync(5);

        Assert.True(viewModel.Pads[5].IsLooping);
    }
    #endregion
}
