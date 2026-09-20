namespace OvertonesPlayground.Tests.ViewModels;

public sealed class MultiTrackViewModelTests
{
    #region Fields
    private readonly IAudioLibraryService _library = Substitute.For<IAudioLibraryService>();
    private readonly IMixdownService _mixdown = Substitute.For<IMixdownService>();
    #endregion

    #region Private methods
    private MultiTrackViewModel Create() => new(_mixdown, _library, NullLoggerFactory.Instance, NullLogger<MultiTrackViewModel>.Instance);

    private static TrackViewModel CreateTrack(Track? track = null) => new(track ?? new Track { Name = "Track 1" }, NullLogger<TrackViewModel>.Instance);
    #endregion

    #region Public methods
    [Theory]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(InvalidDataException))]
    [InlineData(typeof(NotSupportedException))]
    public async Task Bounce_MixFails_ExplainsWhatToTry(Type exceptionType)
    {
        MultiTrackViewModel viewModel = Create();
        _mixdown.RenderAsync(default!, default!).ReturnsForAnyArgs(Task.FromException<string>((Exception)Activator.CreateInstance(exceptionType)!));

        await viewModel.BounceCommand.ExecuteAsync(null);

        Assert.Equal("Couldn't bounce the project - add at least one clip to an unmuted track.", viewModel.StatusMessage);
        Assert.False(viewModel.IsBusy);
        await _library.DidNotReceiveWithAnyArgs().AddClipAsync(default!, default!, default);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("My mix", true)]
    public void Bounce_NeedsAProjectName(string name, bool allowed)
    {
        MultiTrackViewModel viewModel = Create();
        int canExecuteChanged = 0;
        viewModel.BounceCommand.CanExecuteChanged += (_, _) => canExecuteChanged++;

        viewModel.ProjectName = name;

        Assert.Equal(allowed, viewModel.BounceCommand.CanExecute(null));
        Assert.True(canExecuteChanged > 0);
    }

    [Fact]
    public async Task Bounce_RendersTheTracksAndSavesTheMixToTheLibrary()
    {
        MultiTrackViewModel viewModel = Create();
        viewModel.ProjectName = "My mix";
        _mixdown.RenderAsync(Arg.Is<MixProject>(p => p.Name == "My mix" && p.Tracks.Count == 4), "My mix").Returns("/mixes/mix.wav");
        _library.AddClipAsync("/mixes/mix.wav", "My mix", true).Returns(TestData.Clip("My mix"));

        await viewModel.BounceCommand.ExecuteAsync(null);

        await _library.Received(1).AddClipAsync("/mixes/mix.wav", "My mix", true);
        Assert.Equal("Saved 'My mix' to your library.", viewModel.StatusMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task Bounce_SendsTheTracksOwnModelsSoTheirSettingsAreMixed()
    {
        MultiTrackViewModel viewModel = Create();
        viewModel.Tracks[1].Volume = 0.3;
        viewModel.Tracks[2].IsMuted = true;
        MixProject? rendered = null;
        _mixdown.RenderAsync(Arg.Do<MixProject>(project => rendered = project), Arg.Any<string>()).Returns("/mixes/mix.wav");
        _library.AddClipAsync(default!, default!, default).ReturnsForAnyArgs(TestData.Clip("Mix"));

        await viewModel.BounceCommand.ExecuteAsync(null);

        Assert.NotNull(rendered);
        Assert.Equal(0.3, rendered.Tracks[1].Volume);
        Assert.True(rendered.Tracks[2].IsMuted);
    }

    [Fact]
    public async Task Bounce_WhileAlreadyBouncing_IsIgnored()
    {
        MultiTrackViewModel viewModel = Create();
        viewModel.IsBusy = true;

        await viewModel.BounceCommand.ExecuteAsync(null);

        await _mixdown.DidNotReceiveWithAnyArgs().RenderAsync(default!, default!);
    }

    [Fact]
    public void Constructor_BuildsFourNumberedTracksAndAProjectName()
    {
        MultiTrackViewModel viewModel = Create();

        Assert.Equal("Multi-Track", viewModel.Title);
        Assert.Equal("Mix", viewModel.ProjectName);
        Assert.Equal(["Track 1", "Track 2", "Track 3", "Track 4"], viewModel.Tracks.Select(t => t.Name));
        Assert.Equal([1, 2, 3, 4], viewModel.Tracks.Select(t => t.Number));
    }

    [Fact]
    public async Task GetLibraryClips_ListsTheLibrariesClipsForThePicker()
    {
        AudioClip clip = TestData.Clip("Song");
        _library.GetClipsAsync().Returns(TestData.Clips(clip));

        IReadOnlyList<AudioClip> clips = await Create().GetLibraryClipsAsync();

        Assert.Equal([clip], clips);
    }

    [Fact]
    public void MixAllToStart_MovesEveryClipOnEveryTrackToTheStart()
    {
        MultiTrackViewModel viewModel = Create();
        viewModel.Tracks[0].AddClip(TestData.Clip("A", seconds: 5));
        viewModel.Tracks[0].AddClip(TestData.Clip("B", seconds: 5));
        viewModel.Tracks[3].AddClip(TestData.Clip("C", seconds: 5));
        viewModel.Tracks[3].AddClip(TestData.Clip("D", seconds: 5));

        viewModel.MixAllToStartCommand.Execute(null);

        Assert.All(viewModel.Tracks.SelectMany(t => t.Clips), clip => Assert.Equal(0, clip.StartOffsetSeconds));
        Assert.All(viewModel.Tracks.SelectMany(t => t.Track.Clips), clip => Assert.Equal(TimeSpan.Zero, clip.StartOffset));
    }

    [Fact]
    public void Soloing_OneTrack_DimsTheOthers_AndUnsoloingRestoresThem()
    {
        MultiTrackViewModel viewModel = Create();

        viewModel.Tracks[2].ToggleSoloCommand.Execute(null);
        Assert.Equal([true, true, false, true], viewModel.Tracks.Select(t => t.IsDimmed));

        viewModel.Tracks[2].ToggleSoloCommand.Execute(null);
        Assert.All(viewModel.Tracks, track => Assert.False(track.IsDimmed));
    }

    [Fact]
    public void Track_AddClip_KeepsTheClipsFileAndDuration()
    {
        TrackViewModel track = CreateTrack();

        track.AddClip(TestData.Clip("A", seconds: 3, path: "/clips/a.wav"));

        Assert.Equal("/clips/a.wav", track.Track.Clips[0].ClipFilePath);
        Assert.Equal(TimeSpan.FromSeconds(3), track.Track.Clips[0].Duration);
    }

    [Fact]
    public void Track_AddClip_PlacesTheFirstAtTheStartAndEachNextOneAfterThePrevious()
    {
        TrackViewModel track = CreateTrack();

        track.AddClip(TestData.Clip("A", seconds: 3));
        track.AddClip(TestData.Clip("B", seconds: 4));
        track.AddClip(TestData.Clip("C", seconds: 2));

        Assert.Equal([0.0, 3.0, 7.0], track.Clips.Select(c => c.StartOffsetSeconds));
        Assert.Equal([0.0, 3.0, 7.0], track.Track.Clips.Select(c => c.StartOffset.TotalSeconds));
        Assert.Equal(["A", "B", "C"], track.Track.Clips.Select(c => c.ClipName));
    }

    [Fact]
    public void Track_ChangesFlowBackIntoTheModel()
    {
        TrackViewModel track = CreateTrack();

        track.Name = "Bass";
        track.Volume = 0.4;
        track.Pan = -0.6;
        track.ToggleMuteCommand.Execute(null);
        track.ToggleSoloCommand.Execute(null);

        Assert.Equal("Bass", track.Track.Name);
        Assert.Equal(0.4, track.Track.Volume);
        Assert.Equal(-0.6, track.Track.Pan);
        Assert.True(track.Track.IsMuted);
        Assert.True(track.Track.IsSoloed);
    }

    [Fact]
    public void Track_MergeClips_LaysThemEndToEndFromTheStart()
    {
        TrackViewModel track = CreateTrack();
        track.AddClip(TestData.Clip("A", seconds: 3));
        track.AddClip(TestData.Clip("B", seconds: 4));
        track.Clips[0].StartOffsetSeconds = 10;
        track.Clips[1].StartOffsetSeconds = 20;

        track.MergeClipsCommand.Execute(null);

        Assert.Equal([0.0, 3.0], track.Clips.Select(c => c.StartOffsetSeconds));
    }

    [Fact]
    public void Track_MirrorsItsModelIncludingClipsAlreadyOnIt()
    {
        Track model = new() { Name = "Drums", Volume = 0.5, Pan = 0.2, IsMuted = true, IsSoloed = true };
        model.Clips.Add(new TrackClip { ClipName = "Loop", Duration = TimeSpan.FromSeconds(4) });

        TrackViewModel track = CreateTrack(model);

        Assert.Equal("Drums", track.Name);
        Assert.Equal(0.5, track.Volume);
        Assert.Equal(0.2, track.Pan);
        Assert.True(track.IsMuted);
        Assert.True(track.IsSoloed);
        Assert.Single(track.Clips);
        Assert.Same(model, track.Track);
    }

    [Fact]
    public void Track_RemoveClip_NoClip_DoesNothing()
    {
        TrackViewModel track = CreateTrack();
        track.AddClip(TestData.Clip("A"));

        track.RemoveClipCommand.Execute(null);

        Assert.Single(track.Clips);
    }

    [Fact]
    public void Track_RemoveClip_TakesItOffTheTrackAndTheModel()
    {
        TrackViewModel track = CreateTrack();
        track.AddClip(TestData.Clip("A", seconds: 3));
        track.AddClip(TestData.Clip("B", seconds: 4));
        TrackClipViewModel first = track.Clips[0];

        track.RemoveClipCommand.Execute(first);

        Assert.Single(track.Clips);
        Assert.Equal(["B"], track.Track.Clips.Select(c => c.ClipName));
    }

    [Fact]
    public void TrackClip_GainAndOffset_FlowBackIntoTheModel()
    {
        TrackClip model = new();
        TrackClipViewModel clip = new(model);

        clip.GainDb = 6;
        clip.StartOffsetSeconds = 12.5;

        Assert.Equal(6, model.GainDb);
        Assert.Equal(TimeSpan.FromSeconds(12.5), model.StartOffset);
    }

    [Fact]
    public void TrackClip_MirrorsItsModel()
    {
        TrackClip model = new() { ClipName = "Loop", Duration = TimeSpan.FromSeconds(65), GainDb = -3, StartOffset = TimeSpan.FromSeconds(2) };

        TrackClipViewModel clip = new(model);

        Assert.Equal("Loop", clip.ClipName);
        Assert.Equal("01:05", clip.DurationText);
        Assert.Equal(-3, clip.GainDb);
        Assert.Equal(2, clip.StartOffsetSeconds);
        Assert.Same(model, clip.TrackClip);
    }

    [Fact]
    public void TrackClip_NegativeOffset_IsNeverBeforeTheStartOfTheTrack()
    {
        TrackClip model = new() { StartOffset = TimeSpan.FromSeconds(5) };
        TrackClipViewModel clip = new(model);

        clip.StartOffsetSeconds = -4;

        Assert.Equal(TimeSpan.Zero, model.StartOffset);
    }
    #endregion
}
