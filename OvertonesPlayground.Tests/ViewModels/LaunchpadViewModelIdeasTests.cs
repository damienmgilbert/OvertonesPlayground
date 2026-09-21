namespace OvertonesPlayground.Tests.ViewModels;

///<summary>
///The Launchpad's ideas from the Sound Bank: style presets, generating a project, the key for suggestions, suggestions for a pad
///and a column, and swapping a bank's kit.
///</summary>
public sealed class LaunchpadViewModelIdeasTests : IDisposable
{
    #region Fields
    private static readonly LaunchpadStyleInfo House = new("house", "House", "Four on the floor.", 118, 128);
    private static readonly LaunchpadExampleInfo HousePreset = new("house-909", "House (909) · 124 BPM · F minor", "A house setup.", "house");
    private readonly ILaunchpadExampleService _examples = Substitute.For<ILaunchpadExampleService>();
    private readonly TempFileSystem _files = new();
    private readonly IAudioLibraryService _library = Substitute.For<IAudioLibraryService>();
    private readonly IMixdownService _mixdown = Substitute.For<IMixdownService>();
    private readonly IAudioPlaybackService _playback = Substitute.For<IAudioPlaybackService>();
    private readonly FakePreferences _preferences = new();
    private readonly ISoundSynthesisService _synthesis = Substitute.For<ISoundSynthesisService>();
    #endregion

    #region Constructors
    public LaunchpadViewModelIdeasTests()
    {
        _examples.Styles.Returns([House]);
        _examples.Presets.Returns([HousePreset]);
        _examples.PrepareSampleAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => Task.FromResult(_files.CreateFile($"LaunchpadSamples/{call.ArgAt<string>(0)}.wav")));
    }
    #endregion

    #region Private types
    private sealed class MenuWatcher
    {
        public MenuWatcher(LaunchpadViewModel viewModel) { viewModel.MenuRequested += (_, e) => Menus.Add(e); }

        public Task ChooseAsync(string textStartsWith) => Last.Choices.First(choice => choice.Text.StartsWith(textStartsWith, StringComparison.Ordinal)).Run();

        public LaunchpadMenuEventArgs Last => Menus[^1];

        public List<LaunchpadMenuEventArgs> Menus { get; } = [];
    }
    #endregion

    #region Private methods
    private LaunchpadViewModel Create() => new(_playback, _library, _synthesis, _mixdown, _preferences, _files, _examples, NullLogger<LaunchpadViewModel>.Instance);

    private LaunchpadProject GeneratedProject()
    {
        string kick = _files.CreateFile("LaunchpadSamples/Kick 909.wav");
        LaunchpadProject project = new() { Tempo = 124, ScaleIndex = 1, RootPitchClass = 5, StyleKey = "house", Seed = 99, Origin = LaunchpadProjectOrigin.Generated };
        project.Pads.Add(new LaunchpadPad { Bank = 0, Index = 56, ClipPath = kick, Label = "Kick 909" });
        return project;
    }

    private static void Press(LaunchpadViewModel viewModel, LaunchpadControl control) => viewModel.PressKey(viewModel.AllKeys.First(key => key.Control == control));
    #endregion

    #region Public methods
    [Fact]
    public async Task Projects_ReadyMadeByStyle_ListsTheStylesPresetsAndLoadsTheOnePicked()
    {
        _examples.CreateAsync("house-909", Arg.Any<CancellationToken>()).Returns(GeneratedProject());
        LaunchpadViewModel viewModel = Create();
        MenuWatcher menu = new(viewModel);
        Press(viewModel, LaunchpadControl.Projects);

        await menu.ChooseAsync("Ready-made setups by style");
        await menu.ChooseAsync("House");
        Assert.Equal(["House (909) · 124 BPM · F minor"], menu.Last.Choices.Select(choice => choice.Text));
        await menu.ChooseAsync("House (909)");

        Assert.True(viewModel.Pads[56].HasClip);
        Assert.StartsWith("Loaded 'House (909)", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Projects_Generate_LoadsTheGeneratedProjectAndDescribesIt()
    {
        _examples.GenerateAsync(Arg.Any<LaunchpadGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LaunchpadGeneratedProject(GeneratedProject(), "House · 124 BPM · F minor", "Bank A: drums."));
        LaunchpadViewModel viewModel = Create();
        MenuWatcher menu = new(viewModel);
        Press(viewModel, LaunchpadControl.Projects);

        await menu.ChooseAsync("Generate a new project");
        await menu.ChooseAsync("House");

        _ = await _examples.Received(1).GenerateAsync(Arg.Is<LaunchpadGenerationRequest>(request => request.StyleKey == "house"), Arg.Any<CancellationToken>());
        Assert.True(viewModel.Pads[56].HasClip);
        LaunchpadProject saved = System.Text.Json.JsonSerializer.Deserialize<LaunchpadProject>(File.ReadAllText(_files.InAppData("launchpad.json")))!;
        Assert.Equal(124, saved.Tempo);
        Assert.Equal(5, saved.RootPitchClass);
        Assert.Equal("house", saved.StyleKey);
        Assert.Equal(99, saved.Seed);
        Assert.Equal(LaunchpadProjectOrigin.Generated, saved.Origin);
        Assert.StartsWith("Generated 'House · 124 BPM · F minor'. Bank A: drums.", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task Projects_AfterGenerating_OffersAnotherInTheSameStyle()
    {
        _examples.GenerateAsync(Arg.Any<LaunchpadGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LaunchpadGeneratedProject(GeneratedProject(), "House", "Drums."));
        LaunchpadViewModel viewModel = Create();
        MenuWatcher menu = new(viewModel);
        await viewModel.GenerateAsync("house");

        Press(viewModel, LaunchpadControl.Projects);

        Assert.Contains(menu.Last.Choices, choice => choice.Text == "Another House project");
    }

    [Fact]
    public async Task Projects_GenerateFails_SaysSoAndKeepsThePads()
    {
        _examples.GenerateAsync(Arg.Any<LaunchpadGenerationRequest>(), Arg.Any<CancellationToken>()).Returns<LaunchpadGeneratedProject>(_ => throw new InvalidOperationException("no"));
        LaunchpadViewModel viewModel = Create();

        await viewModel.GenerateAsync("house");

        Assert.Equal("Couldn't generate a House project.", viewModel.StatusMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task Setup_Key_CyclesThroughTheRootsAndBackToNone()
    {
        LaunchpadViewModel viewModel = Create();
        MenuWatcher menu = new(viewModel);

        Press(viewModel, LaunchpadControl.Setup);
        Assert.Contains(menu.Last.Choices, choice => choice.Text.StartsWith("Key for suggestions: none", StringComparison.Ordinal));
        await menu.ChooseAsync("Key for suggestions");

        Assert.Equal("Key: C major. Suggestions keep to it.", viewModel.StatusMessage);
        for (int i = 0; i < 12; i++)
        {
            Press(viewModel, LaunchpadControl.Setup);
            await menu.ChooseAsync("Key for suggestions");
        }

        Assert.StartsWith("Key: none.", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SuggestForPad_ListsTheSuggestionsWithReasonsAndPutsThePickedOneOnThePad()
    {
        _examples.SuggestAsync(Arg.Any<LaunchpadProject>(), Arg.Is(0), Arg.Is(3), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new LaunchpadSuggestion("Hihat Closed Vinyl 1", ["same kit (vinyl)"]), new LaunchpadSuggestion("Hihat Closed 909", [])]);
        LaunchpadViewModel viewModel = Create();
        MenuWatcher menu = new(viewModel);

        await viewModel.SuggestForPadAsync(viewModel.Pads[3]);
        Assert.Equal(["Hihat Closed Vinyl 1 · same kit (vinyl)", "Hihat Closed 909"], menu.Last.Choices.Select(choice => choice.Text));
        await menu.ChooseAsync("Hihat Closed Vinyl 1");

        Assert.True(viewModel.Pads[3].HasClip);
        Assert.Equal("Hihat Closed Vinyl 1", viewModel.Pads[3].Label);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task SuggestForPad_NothingSuits_SaysSo()
    {
        _examples.SuggestAsync(Arg.Any<LaunchpadProject>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);
        LaunchpadViewModel viewModel = Create();

        await viewModel.SuggestForPadAsync(viewModel.Pads[0]);

        Assert.StartsWith("The Sound Bank has nothing that suits", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FillColumn_PutsEachSuggestionOnItsPadAndUndoEmptiesThemAgain()
    {
        _examples.SuggestColumnAsync(Arg.Any<LaunchpadProject>(), 0, 2, Arg.Any<CancellationToken>())
            .Returns([new LaunchpadPadAssignment(58, "Clap A"), new LaunchpadPadAssignment(50, "Clap B")]);
        LaunchpadViewModel viewModel = Create();

        await viewModel.FillColumnAsync(viewModel.Pads[2]);

        Assert.Equal("Clap A", viewModel.Pads[58].Label);
        Assert.Equal("Clap B", viewModel.Pads[50].Label);
        Assert.StartsWith("Filled 2 pads of column 3", viewModel.StatusMessage, StringComparison.Ordinal);

        Press(viewModel, LaunchpadControl.Shift);
        Press(viewModel, LaunchpadControl.RecordArm);

        Assert.False(viewModel.Pads[58].HasClip);
        Assert.False(viewModel.Pads[50].HasClip);
    }

    [Fact]
    public async Task Setup_SwapKit_ReplacesTheDrumsWithTheKitsSounds()
    {
        _examples.GetKitsAsync(Arg.Any<CancellationToken>()).Returns([new LaunchpadKitInfo("909", "Roland TR-909", 88)]);
        _examples.SuggestKitSwapAsync(Arg.Any<LaunchpadProject>(), 0, "909", Arg.Any<CancellationToken>())
            .Returns([new LaunchpadPadAssignment(56, "Kick 909 Tune1 k")]);
        LaunchpadViewModel viewModel = Create();
        MenuWatcher menu = new(viewModel);

        Press(viewModel, LaunchpadControl.Setup);
        await menu.ChooseAsync("Swap bank A's drums");
        await menu.ChooseAsync("Roland TR-909");

        Assert.Equal("Kick 909 Tune1 k", viewModel.Pads[56].Label);
        Assert.StartsWith("Swapped 1 drum on bank A for Roland TR-909 sounds.", viewModel.StatusMessage, StringComparison.Ordinal);
    }

    public void Dispose() => _files.Dispose();
    #endregion
}
