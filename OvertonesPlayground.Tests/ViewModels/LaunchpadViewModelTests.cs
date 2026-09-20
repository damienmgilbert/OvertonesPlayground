namespace OvertonesPlayground.Tests.ViewModels;

public sealed class LaunchpadViewModelTests : IDisposable
{
    #region Fields
    private static readonly LaunchpadExampleInfo Demo = new("demo", "Demo · 128 BPM", "A demo setup.");
    private readonly ILaunchpadExampleService _examples = Substitute.For<ILaunchpadExampleService>();
    private readonly TempFileSystem _files = new();
    private readonly IAudioLibraryService _library = Substitute.For<IAudioLibraryService>();
    private readonly IMixdownService _mixdown = Substitute.For<IMixdownService>();
    private readonly IAudioPlaybackService _playback = Substitute.For<IAudioPlaybackService>();
    private readonly FakePreferences _preferences = new();
    private readonly ISoundSynthesisService _synthesis = Substitute.For<ISoundSynthesisService>();
    #endregion

    #region Private methods
    ///<summary>
    ///Gives pad <paramref name="index"/> a sample through the same command the page uses, with a real file behind it so
    ///the layout can be restored later.
    ///</summary>
    private async Task<AudioClip> AssignAsync(LaunchpadViewModel viewModel, int index, string name = "Kick")
    {
        AudioClip clip = TestData.Clip(name, path: _files.CreateFile($"clips/{name}.wav"));
        _library.ImportFromPickerAsync().Returns(clip);
        await viewModel.AssignCommand.ExecuteAsync(viewModel.Pads[index]);
        return clip;
    }

    private LaunchpadViewModel Create() => new(_playback, _library, _synthesis, _mixdown, _preferences, _files, _examples, NullLogger<LaunchpadViewModel>.Instance);

    private LaunchpadProject DemoProject()
    {
        string kick = _files.CreateFile("LaunchpadSamples/Kick.wav");
        string loop = _files.CreateFile("LaunchpadSamples/Loop.wav");
        LaunchpadProject project = new() { Tempo = 128, IsRadioOn = true, ScaleIndex = 1 };
        project.Pads.Add(new LaunchpadPad { Bank = 0, Index = 56, ClipPath = kick, Label = "Kick", Volume = 0.6 });
        project.Pads.Add(new LaunchpadPad { Bank = 1, Index = 57, ClipPath = loop, Label = "Loop", IsLooping = true });
        project.Columns[3].Pan = 0.2;
        project.Sequence.Tracks[0] = new LaunchpadTrack { ClipPath = kick, Label = "Kick", Column = 0 };
        return project;
    }

    private static LaunchpadKeyViewModel Key(LaunchpadViewModel viewModel, LaunchpadControl control) => viewModel.AllKeys.First(key => key.Control == control);

    private static void NameProjectsWith(LaunchpadViewModel viewModel, string name) => viewModel.TextPrompt = (_, _, _) => Task.FromResult<string?>(name);

    private static void Press(LaunchpadViewModel viewModel, LaunchpadControl control) => viewModel.PressKey(Key(viewModel, control));

    private LaunchpadProject SavedLayout() => System.Text.Json.JsonSerializer.Deserialize<LaunchpadProject>(File.ReadAllText(_files.InAppData("launchpad.json")))!;
    #endregion

    #region Public methods
    [Fact]
    public async Task Assign_GivesThePadTheChosenSampleInItsColumnsColor()
    {
        LaunchpadViewModel viewModel = Create();

        AudioClip clip = await AssignAsync(viewModel, index: 2, name: "Snare");

        LaunchpadPadViewModel pad = viewModel.Pads[2];
        Assert.True(pad.HasClip);
        Assert.Equal("Snare", pad.Label);
        Assert.Equal(clip.FilePath, pad.Pad.ClipPath);
        Assert.Equal("#82C2EE", pad.Pad.ColorHex); // the third column's color
    }

    [Fact]
    public async Task Assign_ImportFails_SaysSoAndLeavesThePadEmpty()
    {
        LaunchpadViewModel viewModel = Create();
        _library.ImportFromPickerAsync().Returns<AudioClip?>(_ => throw new IOException());

        await viewModel.AssignCommand.ExecuteAsync(viewModel.Pads[0]);

        Assert.Equal("Couldn't import that sample.", viewModel.StatusMessage);
        Assert.False(viewModel.Pads[0].HasClip);
    }

    [Fact]
    public async Task Assign_NoPad_DoesNotOpenThePicker()
    {
        await Create().AssignCommand.ExecuteAsync(null);

        await _library.DidNotReceiveWithAnyArgs().ImportFromPickerAsync();
    }

    [Fact]
    public async Task Assign_PickerCancelled_LeavesThePadEmpty()
    {
        LaunchpadViewModel viewModel = Create();
        _library.ImportFromPickerAsync().Returns((AudioClip?)null);

        await viewModel.AssignCommand.ExecuteAsync(viewModel.Pads[0]);

        Assert.False(viewModel.Pads[0].HasClip);
    }

    [Fact]
    public void BankButtons_ShowThatBanksPadsAndSayWhichBank()
    {
        LaunchpadViewModel viewModel = Create();

        Press(viewModel, LaunchpadControl.Right);

        Assert.All(viewModel.Pads, pad => Assert.Equal(1, pad.Pad.Bank));
        Assert.Equal("Bank B.", viewModel.StatusMessage);
    }

    [Fact]
    public void BankButtons_StepThroughTheFourBanksAndWrapAround()
    {
        LaunchpadViewModel viewModel = Create();
        List<int> seen = [];

        for (int i = 0; i < 5; i++)
        {
            Press(viewModel, LaunchpadControl.Right);
            seen.Add(viewModel.Bank);
        }

        Assert.Equal([1, 2, 3, 0, 1], seen);

        Press(viewModel, LaunchpadControl.Left);
        Press(viewModel, LaunchpadControl.Left);
        Press(viewModel, LaunchpadControl.Left);

        Assert.Equal(2, viewModel.Bank); // 1 -> 0 -> 3 -> 2
    }

    [Fact]
    public void ClearButton_ArmsTheClearToolAndPressingItAgainPutsItDown()
    {
        LaunchpadViewModel viewModel = Create();

        Press(viewModel, LaunchpadControl.Clear);
        Assert.Equal(LaunchpadTool.Clear, viewModel.Tool);
        Assert.True(Key(viewModel, LaunchpadControl.Clear).IsLit);

        Press(viewModel, LaunchpadControl.Clear);
        Assert.Equal(LaunchpadTool.None, viewModel.Tool);
    }

    [Fact]
    public async Task ClearPad_EmptiesItAndSilencesItsVoices()
    {
        LaunchpadViewModel viewModel = Create();
        await AssignAsync(viewModel, index: 5);
        int voiceKey = viewModel.Pads[5].Pad.VoiceKey;

        viewModel.ClearPadCommand.Execute(viewModel.Pads[5]);

        Assert.False(viewModel.Pads[5].HasClip);
        _playback.Received(1).StopPad(voiceKey);
    }

    [Fact]
    public void ClearPad_NoPad_DoesNothing()
    {
        Create().ClearPadCommand.Execute(null);

        _playback.DidNotReceiveWithAnyArgs().StopPad(default);
    }

    [Fact]
    public void ClearTool_TappingAnEmptyPad_SaysItIsAlreadyEmpty()
    {
        LaunchpadViewModel viewModel = Create();
        Press(viewModel, LaunchpadControl.Clear);

        viewModel.PadTapped(viewModel.Pads[3]);

        Assert.Equal("That pad is already empty.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task ClearTool_ThenTappingAPadWithASample_ClearsIt()
    {
        LaunchpadViewModel viewModel = Create();
        await AssignAsync(viewModel, index: 3);
        Press(viewModel, LaunchpadControl.Clear);

        viewModel.PadTapped(viewModel.Pads[3]);

        Assert.False(viewModel.Pads[3].HasClip);
    }

    [Fact]
    public void Constructor_BuildsAnEmptyEightByEightGridInSessionMode()
    {
        LaunchpadViewModel viewModel = Create();

        Assert.Equal("Launchpad", viewModel.Title);
        Assert.Equal(64, viewModel.Pads.Count);
        Assert.Equal(LaunchpadViewModel.Rows * LaunchpadViewModel.Columns, viewModel.Pads.Count);
        Assert.All(viewModel.Pads, pad => Assert.False(pad.HasClip));
        Assert.All(viewModel.Pads, pad => Assert.Equal("Empty", pad.Label));
        Assert.Equal(LaunchpadMode.Session, viewModel.Mode);
        Assert.Equal(0, viewModel.Bank);
        Assert.False(viewModel.IsPlaying);
        Assert.False(viewModel.IsEditMode);
        Assert.False(viewModel.IsShiftLatched);
    }

    [Fact]
    public void Constructor_BuildsEveryButtonOfTheRing()
    {
        LaunchpadViewModel viewModel = Create();

        foreach (LaunchpadControl control in Enum.GetValues<LaunchpadControl>())
        {
            Assert.Contains(viewModel.AllKeys, key => key.Control == control);
        }

        Assert.Equal(Enumerable.Range(0, 8), viewModel.TrackKeys.Select(key => key.Column));
        Assert.Equal(viewModel.AllKeys.Count(), viewModel.AllKeys.Select(key => key.Id).Distinct().Count()); // ids double as automation ids
    }

    [Fact]
    public void Constructor_PadsKnowTheirRowAndColumn()
    {
        LaunchpadViewModel viewModel = Create();

        Assert.Equal((0, 0), (viewModel.Pads[0].Row, viewModel.Pads[0].Column));
        Assert.Equal((1, 2), (viewModel.Pads[10].Row, viewModel.Pads[10].Column));
        Assert.Equal((7, 7), (viewModel.Pads[63].Row, viewModel.Pads[63].Column));
    }

    [Fact]
    public async Task CustomMode_ATapAloneDoesNotSound_OnlyHoldingDoes()
    {
        LaunchpadViewModel viewModel = Create();
        await AssignAsync(viewModel, index: 6);
        Press(viewModel, LaunchpadControl.Custom);

        viewModel.PadTapped(viewModel.Pads[6]);

        _playback.DidNotReceiveWithAnyArgs().TriggerVoice(default, default!, default);
    }

    [Fact]
    public async Task CustomMode_PadsSoundWhileHeldAndStopWhenLetGo()
    {
        LaunchpadViewModel viewModel = Create();
        AudioClip clip = await AssignAsync(viewModel, index: 6);
        Press(viewModel, LaunchpadControl.Custom);

        viewModel.PadPressed(viewModel.Pads[6]);
        _playback.Received(1).TriggerVoice(viewModel.Pads[6].Pad.VoiceKey, clip.FilePath, Arg.Any<PadVoiceOptions>());

        viewModel.PadReleased(viewModel.Pads[6]);
        _playback.Received(1).StopPad(viewModel.Pads[6].Pad.VoiceKey);
    }

    public void Dispose() => _files.Dispose();

    [Fact]
    public void EditMode_Toggled_ChangesTheToolbarButtonAndEveryPad()
    {
        LaunchpadViewModel viewModel = Create();

        viewModel.ToggleEditModeCommand.Execute(null);

        Assert.True(viewModel.IsEditMode);
        Assert.Equal("Done", viewModel.EditModeText);
        Assert.Equal(IconFont.Check, viewModel.EditModeGlyph);
        Assert.All(viewModel.Pads, pad => Assert.True(pad.IsEditMode));

        viewModel.ToggleEditModeCommand.Execute(null);

        Assert.Equal("Edit", viewModel.EditModeText);
        Assert.Equal(IconFont.Edit, viewModel.EditModeGlyph);
        Assert.All(viewModel.Pads, pad => Assert.False(pad.IsEditMode));
    }

    [Fact]
    public async Task Examples_ChoosingOne_LoadsItsPadsMixerSequencerRadioAndTempo()
    {
        _examples.Examples.Returns([Demo]);
        _examples.CreateAsync("demo", Arg.Any<CancellationToken>()).Returns(DemoProject());
        LaunchpadViewModel viewModel = Create();
        MenuWatcher menu = new(viewModel);
        Press(viewModel, LaunchpadControl.Projects);
        await menu.ChooseAsync("Examples");

        await menu.ChooseAsync("Demo");

        Assert.True(viewModel.Pads[56].HasClip);
        Assert.Equal("Kick", viewModel.Pads[56].Label);
        Assert.Equal(0.6, viewModel.Pads[56].Pad.Volume);
        Assert.False(viewModel.IsBusy);
        Assert.Equal(LaunchpadMode.Session, viewModel.Mode);
        Assert.StartsWith("Loaded 'Demo · 128 BPM'. A demo setup. Undo brings back", viewModel.StatusMessage);

        // Everything is saved with the layout, so it survives the app being closed.
        LaunchpadProject saved = SavedLayout();
        Assert.Equal(128, saved.Tempo);
        Assert.True(saved.IsRadioOn);
        Assert.Equal(2, saved.Pads.Count);
        Assert.Contains(saved.Pads, pad => pad is { Bank: 1, Index: 57, IsLooping: true });
        Assert.Equal(0.2, saved.Columns[3].Pan);
        Assert.True(saved.Sequence.Tracks[0].HasSource);
    }

    [Fact]
    public async Task Examples_LoadedWhilePlaying_StopsTheSoundsFirst()
    {
        _examples.Examples.Returns([Demo]);
        _examples.CreateAsync("demo", Arg.Any<CancellationToken>()).Returns(DemoProject());
        LaunchpadViewModel viewModel = Create();
        MenuWatcher menu = new(viewModel);
        Press(viewModel, LaunchpadControl.Projects);
        await menu.ChooseAsync("Examples");

        await menu.ChooseAsync("Demo");

        _playback.Received().StopAllPads();
    }

    [Fact]
    public async Task Examples_LoadedWhileSomethingIsOnTheGrid_CanBeUndone()
    {
        _examples.Examples.Returns([Demo]);
        _examples.CreateAsync("demo", Arg.Any<CancellationToken>()).Returns(DemoProject());
        LaunchpadViewModel viewModel = Create();
        await AssignAsync(viewModel, index: 0, name: "Mine");
        MenuWatcher menu = new(viewModel);
        Press(viewModel, LaunchpadControl.Projects);
        await menu.ChooseAsync("Examples");
        await menu.ChooseAsync("Demo");
        Assert.False(viewModel.Pads[0].HasClip);

        Press(viewModel, LaunchpadControl.Shift);
        Press(viewModel, LaunchpadControl.RecordArm);

        Assert.True(viewModel.Pads[0].HasClip);
        Assert.Equal("Mine", viewModel.Pads[0].Label);
        Assert.False(viewModel.Pads[56].HasClip);
    }

    [Fact]
    public async Task Examples_TheMenuListsTheSetupsTheServiceOffers()
    {
        _examples.Examples.Returns([Demo]);
        LaunchpadViewModel viewModel = Create();
        MenuWatcher menu = new(viewModel);
        Press(viewModel, LaunchpadControl.Projects);

        await menu.ChooseAsync("Examples");

        Assert.Equal("Load an example setup", menu.Last.Title);
        Assert.Equal(["Demo · 128 BPM"], menu.Last.Choices.Select(choice => choice.Text));
    }

    [Theory]
    [InlineData(typeof(InvalidDataException))]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(InvalidOperationException))]
    public async Task Examples_TheServiceFails_SaysSoAndLeavesTheGridAlone(Type exceptionType)
    {
        _examples.Examples.Returns([Demo]);
        _examples.CreateAsync("demo", Arg.Any<CancellationToken>()).Returns<Task<LaunchpadProject>>(_ => throw (Exception)Activator.CreateInstance(exceptionType)!);
        LaunchpadViewModel viewModel = Create();
        await AssignAsync(viewModel, index: 0, name: "Mine");
        MenuWatcher menu = new(viewModel);
        Press(viewModel, LaunchpadControl.Projects);
        await menu.ChooseAsync("Examples");

        await menu.ChooseAsync("Demo");

        Assert.Equal("Couldn't load 'Demo · 128 BPM'.", viewModel.StatusMessage);
        Assert.True(viewModel.Pads[0].HasClip);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task Layout_APadWhoseSampleFileIsGone_StaysEmpty()
    {
        LaunchpadViewModel first = Create();
        AudioClip kept = await AssignAsync(first, index: 0, name: "Kept");
        AudioClip lost = await AssignAsync(first, index: 1, name: "Lost");
        File.Delete(lost.FilePath);

        LaunchpadViewModel second = Create();

        Assert.True(second.Pads[0].HasClip);
        Assert.Equal(kept.FilePath, second.Pads[0].Pad.ClipPath);
        Assert.False(second.Pads[1].HasClip);
    }

    [Fact]
    public void Layout_CorruptFile_IsIgnored()
    {
        _files.CreateFile("launchpad.json", "{ this is not json");

        LaunchpadViewModel viewModel = Create();

        Assert.Equal(64, viewModel.Pads.Count);
        Assert.All(viewModel.Pads, pad => Assert.False(pad.HasClip));
    }

    [Fact]
    public void Layout_FromTheFirstVersion_ABareListOfPads_BecomesBankA()
    {
        string sample = _files.CreateFile("clips/old.wav");
        string json = $$"""[{"Bank":0,"Index":3,"ClipPath":{{System.Text.Json.JsonSerializer.Serialize(sample)}},"Label":"Old","IsLooping":true}]""";
        _files.CreateFile("launchpad.json", json);

        LaunchpadViewModel viewModel = Create();

        Assert.True(viewModel.Pads[3].HasClip);
        Assert.Equal("Old", viewModel.Pads[3].Label);
        Assert.True(viewModel.Pads[3].IsLooping);
        Assert.Equal(0, viewModel.Bank);
    }

    [Fact]
    public async Task Layout_IsSavedAfterEveryChangeAndRestoredNextTime()
    {
        LaunchpadViewModel first = Create();
        AudioClip clip = await AssignAsync(first, index: 7, name: "Hat");
        first.ToggleLoopCommand.Execute(first.Pads[7]);

        LaunchpadViewModel second = Create();

        Assert.True(File.Exists(_files.InAppData("launchpad.json")));
        Assert.True(second.Pads[7].HasClip);
        Assert.Equal("Hat", second.Pads[7].Label);
        Assert.Equal(clip.FilePath, second.Pads[7].Pad.ClipPath);
        Assert.True(second.Pads[7].IsLooping);
        Assert.False(second.Pads[6].HasClip);
    }

    [Fact]
    public async Task Layout_RestoredForEveryBankAndTheBankThatWasShowing()
    {
        LaunchpadViewModel first = Create();
        await AssignAsync(first, index: 0, name: "A");
        Press(first, LaunchpadControl.Right);
        Press(first, LaunchpadControl.Right);
        await AssignAsync(first, index: 1, name: "C");

        LaunchpadViewModel second = Create();

        Assert.Equal(2, second.Bank);
        Assert.Equal("C", second.Pads[1].Label);
        Press(second, LaunchpadControl.Left);
        Press(second, LaunchpadControl.Left);
        Assert.Equal("A", second.Pads[0].Label);
    }

    [Fact]
    public async Task Layout_UnwritableStorage_DoesNotBreakEditing()
    {
        LaunchpadViewModel viewModel = Create();
        // A folder where the layout file should be makes every save fail with an IOException/UnauthorizedAccessException.
        Directory.CreateDirectory(_files.InAppData("launchpad.json"));

        await AssignAsync(viewModel, index: 0);

        Assert.True(viewModel.Pads[0].HasClip);
    }

    [Theory]
    [InlineData(LaunchpadControl.Session, LaunchpadMode.Session)]
    [InlineData(LaunchpadControl.Note, LaunchpadMode.Note)]
    [InlineData(LaunchpadControl.Chord, LaunchpadMode.Chord)]
    [InlineData(LaunchpadControl.Custom, LaunchpadMode.Custom)]
    [InlineData(LaunchpadControl.Sequencer, LaunchpadMode.Sequencer)]
    public void ModeButtons_ChooseWhatThePadsDo(LaunchpadControl button, LaunchpadMode expected)
    {
        LaunchpadViewModel viewModel = Create();

        Press(viewModel, button);

        Assert.Equal(expected, viewModel.Mode);
        Assert.True(Key(viewModel, button).IsLit);
    }

    [Theory]
    [InlineData(LaunchpadControl.Note)]
    [InlineData(LaunchpadControl.Chord)]
    public void NoteAndChordModes_WithNothingPlayedYet_ExplainWhy(LaunchpadControl button)
    {
        LaunchpadViewModel viewModel = Create();

        Press(viewModel, button);

        Assert.StartsWith("Play a pad in Session mode first", viewModel.StatusMessage);
    }

    [Fact]
    public void OpenPadMenu_AsksThePageToShowThatPadsMenu()
    {
        LaunchpadViewModel viewModel = Create();
        LaunchpadPadViewModel? requested = null;
        viewModel.PadMenuRequested += (_, e) => requested = e.Pad;

        viewModel.OpenPadMenuCommand.Execute(viewModel.Pads[9]);

        Assert.Same(viewModel.Pads[9], requested);
    }

    [Fact]
    public void OpenPadMenu_NoPad_RaisesNothing()
    {
        LaunchpadViewModel viewModel = Create();
        bool raised = false;
        viewModel.PadMenuRequested += (_, _) => raised = true;

        viewModel.OpenPadMenuCommand.Execute(null);

        Assert.False(raised);
    }

    [Fact]
    public void PadTapped_EmptyPad_PlaysNothing()
    {
        LaunchpadViewModel viewModel = Create();

        viewModel.PadTapped(viewModel.Pads[0]);

        _playback.DidNotReceiveWithAnyArgs().TriggerVoice(default, default!, default);
    }

    [Fact]
    public async Task PadTapped_InEditMode_OpensThePadMenuInsteadOfPlaying()
    {
        LaunchpadViewModel viewModel = Create();
        await AssignAsync(viewModel, index: 4);
        viewModel.ToggleEditModeCommand.Execute(null);
        LaunchpadPadViewModel? requested = null;
        viewModel.PadMenuRequested += (_, e) => requested = e.Pad;

        viewModel.PadTapped(viewModel.Pads[4]);

        Assert.Same(viewModel.Pads[4], requested);
        _playback.DidNotReceiveWithAnyArgs().TriggerVoice(default, default!, default);
    }

    [Fact]
    public async Task PadTapped_LoopingPad_StartsALoopingVoice()
    {
        LaunchpadViewModel viewModel = Create();
        await AssignAsync(viewModel, index: 4);
        viewModel.ToggleLoopCommand.Execute(viewModel.Pads[4]);

        viewModel.PadTapped(viewModel.Pads[4]);

        _playback.Received(1).TriggerVoice(Arg.Any<int>(), Arg.Any<string>(), Arg.Is<PadVoiceOptions>(options => options.Loop));
    }

    [Fact]
    public void PadTapped_NoPad_DoesNothing()
    {
        Create().PadTapped(null);

        _playback.DidNotReceiveWithAnyArgs().TriggerVoice(default, default!, default);
    }

    [Fact]
    public async Task PadTapped_PadWithASample_StartsItsVoiceAtNormalVolumeAndSpeed()
    {
        LaunchpadViewModel viewModel = Create();
        AudioClip clip = await AssignAsync(viewModel, index: 4);

        viewModel.PadTapped(viewModel.Pads[4]);

        _playback.Received(1).TriggerVoice(viewModel.Pads[4].Pad.VoiceKey, clip.FilePath, Arg.Is<PadVoiceOptions>(options => options.Volume == 1 && options.Balance == 0 && options.Speed == 1 && !options.Loop && options.MaxLength == null));
    }

    [Fact]
    public void PressKey_NoKey_DoesNothing()
    {
        LaunchpadViewModel viewModel = Create();

        viewModel.PressKey(null);

        Assert.Equal(LaunchpadMode.Session, viewModel.Mode);
    }

    [Fact]
    public async Task Projects_Delete_RemovesTheFileButNotTheCurrentPads()
    {
        LaunchpadViewModel viewModel = Create();
        await AssignAsync(viewModel, index: 0);
        NameProjectsWith(viewModel, "Beat");
        MenuWatcher menu = new(viewModel);
        Press(viewModel, LaunchpadControl.Projects);
        await menu.ChooseAsync("Save as");

        Press(viewModel, LaunchpadControl.Projects);
        await menu.ChooseAsync("Delete");
        await menu.ChooseAsync("Beat");

        Assert.False(File.Exists(_files.InAppData("LaunchpadProjects", "Beat.json")));
        Assert.Equal("Deleted project 'Beat'.", viewModel.StatusMessage);
        Assert.True(viewModel.Pads[0].HasClip);
    }

    [Fact]
    public async Task Projects_New_StartsEmptyAndUndoBringsBackTheOldOne()
    {
        LaunchpadViewModel viewModel = Create();
        await AssignAsync(viewModel, index: 0);
        MenuWatcher menu = new(viewModel);
        Press(viewModel, LaunchpadControl.Projects);

        await menu.ChooseAsync("New project");

        Assert.False(viewModel.Pads[0].HasClip);
        Assert.Equal("New project. Undo brings back what was here before.", viewModel.StatusMessage);
        _playback.Received().StopAllPads();

        Press(viewModel, LaunchpadControl.Shift);
        Press(viewModel, LaunchpadControl.RecordArm);
        Assert.True(viewModel.Pads[0].HasClip);
    }

    [Fact]
    public async Task Projects_Open_BringsBackASavedProjectAndUndoReturnsToWhatWasThere()
    {
        LaunchpadViewModel viewModel = Create();
        await AssignAsync(viewModel, index: 0, name: "Saved");
        NameProjectsWith(viewModel, "Beat");
        MenuWatcher menu = new(viewModel);
        Press(viewModel, LaunchpadControl.Projects);
        await menu.ChooseAsync("Save as");
        viewModel.ClearPadCommand.Execute(viewModel.Pads[0]);
        Assert.False(viewModel.Pads[0].HasClip);

        Press(viewModel, LaunchpadControl.Projects);
        await menu.ChooseAsync("Open");
        await menu.ChooseAsync("Beat");

        Assert.True(viewModel.Pads[0].HasClip);
        Assert.Equal("Saved", viewModel.Pads[0].Label);
        Assert.Equal("Opened 'Beat'. Undo brings back what was here before.", viewModel.StatusMessage);

        Press(viewModel, LaunchpadControl.Shift);
        Press(viewModel, LaunchpadControl.RecordArm);
        Assert.False(viewModel.Pads[0].HasClip);
    }

    [Fact]
    public async Task Projects_OpenListsProjectsInAlphabeticalOrder()
    {
        LaunchpadViewModel viewModel = Create();
        MenuWatcher menu = new(viewModel);
        foreach (string name in new[] { "beta", "Alpha", "gamma" })
        {
            NameProjectsWith(viewModel, name);
            Press(viewModel, LaunchpadControl.Projects);
            await menu.ChooseAsync("Save as");
        }

        Press(viewModel, LaunchpadControl.Projects);
        await menu.ChooseAsync("Open");

        Assert.Equal(["Alpha", "beta", "gamma"], menu.Last.Choices.Select(choice => choice.Text));
    }

    [Fact]
    public async Task Projects_OpenWithNoneSaved_SaysSo()
    {
        LaunchpadViewModel viewModel = Create();
        MenuWatcher menu = new(viewModel);
        Press(viewModel, LaunchpadControl.Projects);

        await menu.ChooseAsync("Open");

        Assert.Equal("No saved projects yet: use Save as...", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Projects_SaveAs_NameCancelled_SavesNothing()
    {
        LaunchpadViewModel viewModel = Create();
        viewModel.TextPrompt = (_, _, _) => Task.FromResult<string?>(null);
        MenuWatcher menu = new(viewModel);
        Press(viewModel, LaunchpadControl.Projects);

        await menu.ChooseAsync("Save as");

        Assert.False(Directory.Exists(_files.InAppData("LaunchpadProjects")) && Directory.GetFiles(_files.InAppData("LaunchpadProjects")).Length > 0);
    }

    [Fact]
    public async Task Projects_SaveAs_NameWithForbiddenCharacters_IsMadeSafeForAFileName()
    {
        LaunchpadViewModel viewModel = Create();
        NameProjectsWith(viewModel, "a/b:c");
        MenuWatcher menu = new(viewModel);
        Press(viewModel, LaunchpadControl.Projects);

        await menu.ChooseAsync("Save as");

        string[] files = Directory.GetFiles(_files.InAppData("LaunchpadProjects"));
        Assert.Single(files);
        Assert.DoesNotContain(Path.GetFileName(files[0]), c => Path.GetInvalidFileNameChars().Contains(c));
    }

    [Fact]
    public async Task Projects_SaveAs_WritesTheProjectAndOffersToResaveItByName()
    {
        LaunchpadViewModel viewModel = Create();
        await AssignAsync(viewModel, index: 0);
        NameProjectsWith(viewModel, "My Beat");
        MenuWatcher menu = new(viewModel);
        Press(viewModel, LaunchpadControl.Projects);

        await menu.ChooseAsync("Save as");

        Assert.True(File.Exists(_files.InAppData("LaunchpadProjects", "My Beat.json")));
        Assert.Equal("Saved project 'My Beat'.", viewModel.StatusMessage);
        Press(viewModel, LaunchpadControl.Projects);
        Assert.Contains(menu.Last.Choices, choice => choice.Text == "Save 'My Beat'");
        Assert.Equal("Projects: My Beat", menu.Last.Title);
    }

    [Fact]
    public void ProjectsButton_OffersSaveOpenExamplesNewAndDelete()
    {
        LaunchpadViewModel viewModel = Create();
        MenuWatcher menu = new(viewModel);

        Press(viewModel, LaunchpadControl.Projects);

        Assert.StartsWith("Projects", menu.Last.Title);
        Assert.Equal(["Save as...", "Open...", "Examples...", "New project", "Delete..."], menu.Last.Choices.Select(choice => choice.Text));
    }

    [Fact]
    public async Task Radio_SavedOn_IsOnAgainWhenTheAppStartsAndStaysOnWhenTheLayoutIsSavedAgain()
    {
        LaunchpadViewModel first = Create();
        Press(first, LaunchpadControl.Shift);
        Press(first, LaunchpadControl.Mute);

        LaunchpadViewModel second = Create();
        await AssignAsync(second, index: 3);

        Assert.True(Key(second, LaunchpadControl.Mute).IsLit);
        Assert.True(SavedLayout().IsRadioOn);
    }

    [Fact]
    public void Radio_TurnedOnWithShiftAndMute_IsSavedWithTheLayout()
    {
        LaunchpadViewModel viewModel = Create();
        Press(viewModel, LaunchpadControl.Shift);
        Press(viewModel, LaunchpadControl.Mute);

        Assert.True(SavedLayout().IsRadioOn);
        Assert.True(Key(viewModel, LaunchpadControl.Mute).IsLit);
    }

    [Fact]
    public async Task SessionMode_HoldingAPadDoesNothing()
    {
        LaunchpadViewModel viewModel = Create();
        await AssignAsync(viewModel, index: 6);

        viewModel.PadPressed(viewModel.Pads[6]);
        viewModel.PadReleased(viewModel.Pads[6]);

        _playback.DidNotReceiveWithAnyArgs().TriggerVoice(default, default!, default);
        _playback.DidNotReceiveWithAnyArgs().StopPad(default);
    }

    [Fact]
    public async Task Setup_ClearBank_EmptiesTheBankOnScreen()
    {
        LaunchpadViewModel viewModel = Create();
        await AssignAsync(viewModel, index: 0);
        await AssignAsync(viewModel, index: 9, name: "Other");
        MenuWatcher menu = new(viewModel);
        Press(viewModel, LaunchpadControl.Setup);

        await menu.ChooseAsync("Clear bank A");

        Assert.All(viewModel.Pads, pad => Assert.False(pad.HasClip));
        Assert.StartsWith("Bank A cleared", viewModel.StatusMessage);
    }

    [Fact]
    public void Setup_Scale_CyclesToTheNextScaleEachTime()
    {
        LaunchpadViewModel viewModel = Create();
        MenuWatcher menu = new(viewModel);
        Press(viewModel, LaunchpadControl.Setup);
        string first = menu.Last.Choices[0].Text;

        menu.Last.Choices[0].Run();
        Press(viewModel, LaunchpadControl.Setup);
        string second = menu.Last.Choices[0].Text;

        Assert.NotEqual(first, second);
        Assert.StartsWith("Scale:", viewModel.StatusMessage);
    }

    [Fact]
    public void Shift_LatchesForOneButtonPressThenReleases()
    {
        LaunchpadViewModel viewModel = Create();

        Press(viewModel, LaunchpadControl.Shift);
        Assert.True(viewModel.IsShiftLatched);
        Assert.True(Key(viewModel, LaunchpadControl.Shift).IsLit);

        Press(viewModel, LaunchpadControl.Note);
        Assert.False(viewModel.IsShiftLatched);
    }

    [Fact]
    public void Shift_PressedTwice_UnlatchesAgain()
    {
        LaunchpadViewModel viewModel = Create();

        Press(viewModel, LaunchpadControl.Shift);
        Press(viewModel, LaunchpadControl.Shift);

        Assert.False(viewModel.IsShiftLatched);
    }

    [Fact]
    public void StopAll_SilencesEveryPad()
    {
        Create().StopAllCommand.Execute(null);

        _playback.Received(1).StopAllPads();
    }

    [Fact]
    public async Task StopPad_SilencesThatPadsVoices()
    {
        LaunchpadViewModel viewModel = Create();
        await AssignAsync(viewModel, index: 8);

        viewModel.StopPadCommand.Execute(viewModel.Pads[8]);

        _playback.Received(1).StopPad(viewModel.Pads[8].Pad.VoiceKey);
    }

    [Fact]
    public void TextPanel_NothingSaved_IsShown()
    {
        LaunchpadViewModel viewModel = Create();

        Assert.True(viewModel.IsTextPanelVisible);
        Assert.Equal("Hide info", viewModel.TextPanelText);
        Assert.Equal(IconFont.Visibility_off, viewModel.TextPanelGlyph);
    }

    [Fact]
    public void TextPanel_SavedAsHidden_StartsHidden()
    {
        _preferences.Set("launchpad_text_panel_visible", false);

        LaunchpadViewModel viewModel = Create();

        Assert.False(viewModel.IsTextPanelVisible);
        Assert.Equal("Show info", viewModel.TextPanelText);
        Assert.Equal(IconFont.Visibility, viewModel.TextPanelGlyph);
    }

    [Fact]
    public void TextPanel_Toggled_IsRememberedForNextTime()
    {
        Create().ToggleTextPanelCommand.Execute(null);

        Assert.False(_preferences.Get("launchpad_text_panel_visible", true));
        Assert.False(Create().IsTextPanelVisible);
    }

    [Fact]
    public void TextPanel_Toggled_RaisesItsGlyphAndText()
    {
        LaunchpadViewModel viewModel = Create();
        List<string?> raised = [];
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        viewModel.ToggleTextPanelCommand.Execute(null);

        Assert.Contains(nameof(LaunchpadViewModel.TextPanelGlyph), raised);
        Assert.Contains(nameof(LaunchpadViewModel.TextPanelText), raised);
    }

    [Fact]
    public async Task ToggleLoop_FlipsWhetherThePadLoops()
    {
        LaunchpadViewModel viewModel = Create();
        await AssignAsync(viewModel, index: 0);

        viewModel.ToggleLoopCommand.Execute(viewModel.Pads[0]);
        Assert.True(viewModel.Pads[0].IsLooping);

        viewModel.ToggleLoopCommand.Execute(viewModel.Pads[0]);
        Assert.False(viewModel.Pads[0].IsLooping);
    }

    [Fact]
    public void TransposeButtons_StopAtAnOctaveEitherWay()
    {
        LaunchpadViewModel viewModel = Create();

        for (int i = 0; i < 15; i++)
        {
            Press(viewModel, LaunchpadControl.Up);
        }

        Assert.Equal("Transpose +12 semitones.", viewModel.StatusMessage);

        for (int i = 0; i < 30; i++)
        {
            Press(viewModel, LaunchpadControl.Down);
        }

        Assert.Equal("Transpose -12 semitones.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Transposing_ChangesThePitchOfEveryPadBySemitones()
    {
        LaunchpadViewModel viewModel = Create();
        await AssignAsync(viewModel, index: 0);
        Press(viewModel, LaunchpadControl.Up);
        Press(viewModel, LaunchpadControl.Up);

        viewModel.PadTapped(viewModel.Pads[0]);

        double expected = Math.Pow(2, 2 / 12.0);
        _playback.Received(1).TriggerVoice(Arg.Any<int>(), Arg.Any<string>(), Arg.Is<PadVoiceOptions>(options => Math.Abs(options.Speed - expected) < 1e-9));
    }

    [Fact]
    public async Task Undo_PutsBackWhatTheLastEditChanged()
    {
        LaunchpadViewModel viewModel = Create();
        await AssignAsync(viewModel, index: 1);
        Assert.True(viewModel.Pads[1].HasClip);

        Press(viewModel, LaunchpadControl.Shift);
        Press(viewModel, LaunchpadControl.RecordArm);

        Assert.False(viewModel.Pads[1].HasClip);
        Assert.Equal("Undone.", viewModel.StatusMessage);
    }

    [Fact]
    public void Undo_WithNothingToUndo_SaysSo()
    {
        LaunchpadViewModel viewModel = Create();

        Press(viewModel, LaunchpadControl.Shift);
        Press(viewModel, LaunchpadControl.RecordArm);

        Assert.Equal("Nothing to undo.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task VoiceKeys_AreDifferentInEveryBank()
    {
        LaunchpadViewModel viewModel = Create();
        await AssignAsync(viewModel, index: 0, name: "A");
        int bankA = viewModel.Pads[0].Pad.VoiceKey;
        Press(viewModel, LaunchpadControl.Right);
        await AssignAsync(viewModel, index: 0, name: "B");
        int bankB = viewModel.Pads[0].Pad.VoiceKey;

        Assert.Equal(0, bankA);
        Assert.Equal(64, bankB);
    }
    #endregion

    private sealed class MenuWatcher
    {
        #region Constructors
        public MenuWatcher(LaunchpadViewModel viewModel) { viewModel.MenuRequested += (_, e) => Menus.Add(e); }
        #endregion

        #region Public methods
        public Task ChooseAsync(string textStartsWith) => Last.Choices.First(choice => choice.Text.StartsWith(textStartsWith, StringComparison.Ordinal)).Run();
        #endregion

        #region Public properties
        public LaunchpadMenuEventArgs Last => Menus[^1];

        public List<LaunchpadMenuEventArgs> Menus { get; } = [];
        #endregion
    }
}
