namespace OvertonesPlayground.Tests.ViewModels;

public sealed class SoundCreatorViewModelTests
{
    private readonly IAudioLibraryService _library = Substitute.For<IAudioLibraryService>();
    private readonly IPermissionsService _permissions = Substitute.For<IPermissionsService>();
    private readonly IAudioRecorderService _recorder = Substitute.For<IAudioRecorderService>();

    private SoundCreatorViewModel Create(bool microphoneAllowed = true)
    {
        _permissions.EnsureMicrophonePermissionAsync().Returns(microphoneAllowed);
        return new(_recorder, _library, _permissions, NullLogger<SoundCreatorViewModel>.Instance);
    }

    /// <summary>
    /// A view model that is part-way through a recording, as if the user had tapped record.
    /// </summary>
    private async Task<SoundCreatorViewModel> RecordingAsync(string? name = null)
    {
        SoundCreatorViewModel viewModel = Create();
        await viewModel.ToggleRecordingCommand.ExecuteAsync(null);
        if (name is not null)
        {
            viewModel.NewClipName = name;
        }

        return viewModel;
    }

    #region Starting
    [Fact]
    public async Task Toggle_WhenIdle_StartsRecordingWithAProposedName()
    {
        SoundCreatorViewModel viewModel = Create();

        await viewModel.ToggleRecordingCommand.ExecuteAsync(null);

        await _recorder.Received(1).StartAsync();
        Assert.True(viewModel.IsRecording);
        Assert.StartsWith("Recording ", viewModel.NewClipName);
        Assert.Null(viewModel.StatusMessage);
    }

    [Fact]
    public async Task Toggle_MicrophonePermissionDenied_ExplainsAndDoesNotRecord()
    {
        SoundCreatorViewModel viewModel = Create(microphoneAllowed: false);

        await viewModel.ToggleRecordingCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsRecording);
        Assert.Equal("Microphone permission is required to record.", viewModel.StatusMessage);
        await _recorder.DidNotReceive().StartAsync();
    }

    [Fact]
    public void Constructor_StartsWithAZeroClockAndTheRecorderTitle()
    {
        SoundCreatorViewModel viewModel = Create();

        Assert.Equal("Audio Recorder", viewModel.Title);
        Assert.Equal("00:00", viewModel.ElapsedText);
        Assert.False(viewModel.IsRecording);
    }

    [Theory]
    [InlineData(0, "00:00")]
    [InlineData(65, "01:05")]
    [InlineData(600, "10:00")]
    public void ElapsedChanged_UpdatesTheClock(double seconds, string expected)
    {
        SoundCreatorViewModel viewModel = Create();

        _recorder.ElapsedChanged += Raise.Event<EventHandler<TimeSpan>>(_recorder, TimeSpan.FromSeconds(seconds));

        Assert.Equal(expected, viewModel.ElapsedText);
    }
    #endregion

    #region Stopping and saving
    [Fact]
    public async Task Toggle_WhileRecording_StopsAndSavesTheTakeToTheLibrary()
    {
        SoundCreatorViewModel viewModel = await RecordingAsync("My take");
        AudioClip recorded = TestData.Clip("raw", path: "/clips/raw.wav");
        _recorder.StopAsync("My take").Returns(recorded);
        _library.AddClipAsync("/clips/raw.wav", "My take", true).Returns(TestData.Clip("My take"));

        await viewModel.ToggleRecordingCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsRecording);
        await _library.Received(1).AddClipAsync("/clips/raw.wav", "My take", true);
        Assert.Equal("Saved 'My take' to your library.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Toggle_TakeAlsoExportedToSharedStorage_NamesTheLocation()
    {
        SoundCreatorViewModel viewModel = await RecordingAsync("My take");
        _recorder.StopAsync("My take").Returns(TestData.Clip("raw"));
        _library.AddClipAsync(default!, default!, default).ReturnsForAnyArgs(TestData.Clip("My take", publicLocation: "Music/take.wav"));

        await viewModel.ToggleRecordingCommand.ExecuteAsync(null);

        Assert.Equal("Saved 'My take' - also in Music/take.wav.", viewModel.StatusMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Toggle_NameLeftBlank_SavesUnderAGeneratedName(string blank)
    {
        SoundCreatorViewModel viewModel = await RecordingAsync(blank);
        _recorder.StopAsync(Arg.Any<string>()).Returns(TestData.Clip("raw"));
        _library.AddClipAsync(default!, default!, default).ReturnsForAnyArgs(TestData.Clip("saved"));

        await viewModel.ToggleRecordingCommand.ExecuteAsync(null);

        await _recorder.Received(1).StopAsync(Arg.Is<string>(name => name.StartsWith("Recording ", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task FinishRecording_WhileRecording_SavesWhatWasCaptured()
    {
        SoundCreatorViewModel viewModel = await RecordingAsync("Half a take");
        _recorder.StopAsync("Half a take").Returns(TestData.Clip("raw", path: "/clips/raw.wav"));
        _library.AddClipAsync(default!, default!, default).ReturnsForAnyArgs(TestData.Clip("Half a take"));

        await viewModel.FinishRecordingAsync();

        Assert.False(viewModel.IsRecording);
        await _library.Received(1).AddClipAsync("/clips/raw.wav", "Half a take", true);
    }

    [Fact]
    public async Task FinishRecording_WhenIdle_DoesNothing()
    {
        SoundCreatorViewModel viewModel = Create();

        await viewModel.FinishRecordingAsync();

        await _recorder.DidNotReceiveWithAnyArgs().StopAsync(default!);
        await _library.DidNotReceiveWithAnyArgs().AddClipAsync(default!, default!, default);
    }
    #endregion

    #region Cancelling
    [Fact]
    public async Task CancelRecording_WhileRecording_DiscardsTheTake()
    {
        SoundCreatorViewModel viewModel = await RecordingAsync();

        await viewModel.CancelRecordingCommand.ExecuteAsync(null);

        await _recorder.Received(1).CancelAsync();
        Assert.False(viewModel.IsRecording);
        Assert.Equal("Recording discarded.", viewModel.StatusMessage);
        await _library.DidNotReceiveWithAnyArgs().AddClipAsync(default!, default!, default);
    }

    [Fact]
    public async Task CancelRecording_WhenIdle_DoesNothing()
    {
        SoundCreatorViewModel viewModel = Create();

        await viewModel.CancelRecordingCommand.ExecuteAsync(null);

        await _recorder.DidNotReceive().CancelAsync();
        Assert.Null(viewModel.StatusMessage);
    }
    #endregion
}
