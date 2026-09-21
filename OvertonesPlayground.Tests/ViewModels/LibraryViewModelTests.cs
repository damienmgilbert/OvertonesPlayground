namespace OvertonesPlayground.Tests.ViewModels;

public sealed class LibraryViewModelTests
{
    #region Fields
    private readonly IAudioLibraryService _library = Substitute.For<IAudioLibraryService>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();
    private readonly IAudioPlaybackService _playback = Substitute.For<IAudioPlaybackService>();
    private readonly FakePreferences _preferences = new();
    #endregion

    #region Private methods
    private LibraryViewModel Create() => new(_library, _playback, _navigation, _preferences, NullLogger<LibraryViewModel>.Instance);
    #endregion

    #region Public methods
    [Fact]
    public void Constructor_NoSavedViewMode_ShowsDetailCards()
    {
        Assert.Equal(LibraryViewMode.Detail, Create().ViewMode);
        Assert.Equal("Library", Create().Title);
    }

    [Theory]
    [InlineData("List", LibraryViewMode.List)]
    [InlineData("Detail", LibraryViewMode.Detail)]
    [InlineData("Tile", LibraryViewMode.Tile)]
    public void Constructor_SavedViewMode_IsRestored(string saved, LibraryViewMode expected)
    {
        _preferences.Set("library_view_mode", saved);

        Assert.Equal(expected, Create().ViewMode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Carousel")]
    [InlineData("7")]
    public void Constructor_SavedViewModeIsNotAChoice_FallsBackToDetail(string saved)
    {
        _preferences.Set("library_view_mode", saved);

        Assert.Equal(LibraryViewMode.Detail, Create().ViewMode);
    }

    [Fact]
    public async Task Delete_NoClip_DoesNothing()
    {
        await Create().DeleteCommand.ExecuteAsync(null);

        await _library.DidNotReceiveWithAnyArgs().DeleteClipAsync(default!);
    }

    [Fact]
    public async Task Delete_RemovesTheClipFromTheLibraryAndTheList()
    {
        AudioClip keep = TestData.Clip("Keep");
        AudioClip drop = TestData.Clip("Drop");
        _library.GetClipsAsync().Returns(TestData.Clips(keep, drop));
        LibraryViewModel viewModel = Create();
        await viewModel.LoadCommand.ExecuteAsync(null);

        await viewModel.DeleteCommand.ExecuteAsync(drop);

        await _library.Received(1).DeleteClipAsync(drop);
        Assert.Equal([keep], viewModel.Clips);
    }

    [Fact]
    public async Task Edit_NoClip_DoesNotNavigate()
    {
        await Create().EditCommand.ExecuteAsync(null);

        await _navigation.DidNotReceiveWithAnyArgs().GoToAsync(default!);
    }

    [Fact]
    public async Task Edit_OpensTheEditorForThatClip()
    {
        AudioClip clip = TestData.Clip("Editable");

        await Create().EditCommand.ExecuteAsync(clip);

        await _navigation.Received(1).GoToAsync($"editor?clipId={clip.Id}");
    }

    [Fact]
    public async Task Export_EncodedButNotSavedToSharedStorage_SaysSo()
    {
        AudioClip clip = TestData.Clip("Beat");
        _library.ExportClipAsync(clip, AudioExportFormat.Aac).Returns((string?)null);
        LibraryViewModel viewModel = Create();

        await viewModel.ExportClipAsync(clip, AudioExportFormat.Aac);

        Assert.Equal("Encoded 'Beat', but couldn't save it to shared storage.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Export_Mp3EncoderMissing_SuggestsAac()
    {
        AudioClip clip = TestData.Clip("Beat");
        _library.ExportClipAsync(clip, AudioExportFormat.Mp3).Returns<string?>(_ => throw new NotSupportedException());
        LibraryViewModel viewModel = Create();

        await viewModel.ExportClipAsync(clip, AudioExportFormat.Mp3);

        Assert.Contains("MP3", viewModel.StatusMessage);
        Assert.Contains("Try AAC instead", viewModel.StatusMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task Export_NoClip_Throws() { await Assert.ThrowsAsync<ArgumentNullException>(() => Create().ExportClipAsync(null!, AudioExportFormat.Aac)); }
    [Fact]
    public async Task Export_OtherFormatFails_GivesAPlainMessage()
    {
        AudioClip clip = TestData.Clip("Beat");
        _library.ExportClipAsync(clip, AudioExportFormat.Aac).Returns<string?>(_ => throw new IOException());
        LibraryViewModel viewModel = Create();

        await viewModel.ExportClipAsync(clip, AudioExportFormat.Aac);

        Assert.Equal("Couldn't export 'Beat'.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Export_Saved_NamesWhereItWent()
    {
        AudioClip clip = TestData.Clip("Beat");
        _library.ExportClipAsync(clip, AudioExportFormat.Aac).Returns("Music/Beat.m4a");
        LibraryViewModel viewModel = Create();

        await viewModel.ExportClipAsync(clip, AudioExportFormat.Aac);

        Assert.Equal("Exported 'Beat' to Music/Beat.m4a.", viewModel.StatusMessage);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task Export_WhileBusy_DoesNothing()
    {
        LibraryViewModel viewModel = Create();
        viewModel.IsBusy = true;

        await viewModel.ExportClipAsync(TestData.Clip("Beat"), AudioExportFormat.Aac);

        await _library.DidNotReceiveWithAnyArgs().ExportClipAsync(default!, default);
    }

    [Fact]
    public async Task Import_ClearsTheLastMessageAndStartsProgressAtZero()
    {
        LibraryViewModel viewModel = Create();
        viewModel.StatusMessage = "Old news";
        string? messageDuringImport = "unset";
        bool importingDuringImport = false;
        string statusDuringImport = string.Empty;
        _library.ImportFromPickerAsync(Arg.Any<IProgress<ImportProgress>>())
            .Returns(
        _ =>
        {
            messageDuringImport = viewModel.StatusMessage;
            importingDuringImport = viewModel.IsImporting;
            statusDuringImport = viewModel.ImportStatus;
            return Task.FromResult<AudioClip?>(null);
        });

        await viewModel.ImportCommand.ExecuteAsync(null);

        Assert.Null(messageDuringImport);
        Assert.True(importingDuringImport);
        Assert.Equal("Preparing the file...", statusDuringImport);
    }

    [Fact]
    public async Task Import_ClipChosen_RefreshesTheListAndFinishes()
    {
        AudioClip imported = TestData.Clip("Imported");
        _library.ImportFromPickerAsync(Arg.Any<IProgress<ImportProgress>>()).Returns(imported);
        _library.GetClipsAsync().Returns(TestData.Clips(imported));
        LibraryViewModel viewModel = Create();

        await viewModel.ImportCommand.ExecuteAsync(null);

        Assert.Equal([imported], viewModel.Clips);
        Assert.False(viewModel.IsImporting);
        Assert.Null(viewModel.StatusMessage);
    }

    [Theory]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(InvalidDataException))]
    [InlineData(typeof(NotSupportedException))]
    [InlineData(typeof(OutOfMemoryException))]
    public async Task Import_FileCantBeRead_ReportsItAndStopsShowingProgress(Type exceptionType)
    {
        Exception failure = (Exception)Activator.CreateInstance(exceptionType)!;
        _library.ImportFromPickerAsync(Arg.Any<IProgress<ImportProgress>>()).Returns<AudioClip?>(_ => throw failure);
        LibraryViewModel viewModel = Create();

        await viewModel.ImportCommand.ExecuteAsync(null);

        Assert.Equal("Couldn't import that file.", viewModel.StatusMessage);
        Assert.False(viewModel.IsImporting);
    }

    [Fact]
    public async Task Import_PickerCancelled_ChangesNothing()
    {
        _library.ImportFromPickerAsync(Arg.Any<IProgress<ImportProgress>>()).Returns((AudioClip?)null);
        LibraryViewModel viewModel = Create();

        await viewModel.ImportCommand.ExecuteAsync(null);

        await _library.DidNotReceive().GetClipsAsync();
        Assert.False(viewModel.IsImporting);
        Assert.Null(viewModel.StatusMessage);
    }

    [Theory]
    [InlineData(ImportStage.Copying, "Copying the file...", 0.25)]
    [InlineData(ImportStage.Decoding, "Converting to WAV...", 0.5)]
    [InlineData(ImportStage.Finishing, "Adding to your library...", 0.95)]
    public async Task Import_Progress_IsShownWithItsStageAndFraction(ImportStage stage, string expectedStage, double fraction)
    {
        LibraryViewModel viewModel = Create();

        // Progress<T> delivers a report on another thread, so wait for the view model to show it before letting the import end.
        TaskCompletionSource shown = new();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LibraryViewModel.ImportStatus) && viewModel.ImportStatus.StartsWith(expectedStage, StringComparison.Ordinal))
            {
                shown.TrySetResult();
            }
        };
        _library.ImportFromPickerAsync(Arg.Any<IProgress<ImportProgress>>())
            .Returns(
        async call =>
        {
            call.Arg<IProgress<ImportProgress>>().Report(new ImportProgress(stage, fraction));
            await shown.Task.WaitAsync(TimeSpan.FromSeconds(10));
            return (AudioClip?)null;
        });

        await viewModel.ImportCommand.ExecuteAsync(null);

        Assert.True(shown.Task.IsCompletedSuccessfully);
        Assert.Equal(fraction, viewModel.ImportFraction);
    }

    [Fact]
    public async Task Load_CalledAgain_ReplacesTheListInsteadOfAppendingToIt()
    {
        AudioClip a = TestData.Clip("A");
        _library.GetClipsAsync().Returns(TestData.Clips(a));
        LibraryViewModel viewModel = Create();

        await viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Clips);
    }

    [Fact]
    public async Task Load_FillsTheListWithTheLibrariesClipsInOrder()
    {
        AudioClip newest = TestData.Clip("Newest");
        AudioClip oldest = TestData.Clip("Oldest");
        _library.GetClipsAsync().Returns(TestData.Clips(newest, oldest));
        LibraryViewModel viewModel = Create();

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Equal([newest, oldest], viewModel.Clips);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task Load_ListAlreadyUpToDate_LeavesTheRowsUntouched()
    {
        AudioClip a = TestData.Clip("A");
        AudioClip b = TestData.Clip("B");
        _library.GetClipsAsync().Returns(TestData.Clips(a, b));
        LibraryViewModel viewModel = Create();
        await viewModel.LoadCommand.ExecuteAsync(null);
        int changes = 0;
        viewModel.Clips.CollectionChanged += (_, _) => changes++;

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Equal(0, changes);
    }

    [Fact]
    public async Task Load_OneNewClip_AddsOnlyThatRow()
    {
        AudioClip a = TestData.Clip("A");
        AudioClip fresh = TestData.Clip("Fresh");
        _library.GetClipsAsync().Returns(TestData.Clips(a), TestData.Clips(fresh, a));
        LibraryViewModel viewModel = Create();
        await viewModel.LoadCommand.ExecuteAsync(null);
        int changes = 0;
        viewModel.Clips.CollectionChanged += (_, _) => changes++;

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Equal([fresh, a], viewModel.Clips);
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task Search_ListsOnlyClipsWhoseNameContainsTheText()
    {
        AudioClip kick = TestData.Clip("Big Kick");
        AudioClip snare = TestData.Clip("Snare");
        _library.GetClipsAsync().Returns(TestData.Clips(kick, snare));
        LibraryViewModel viewModel = Create();
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.SearchText = "kick";

        Assert.Equal([kick], viewModel.Clips);
    }

    [Fact]
    public async Task Search_NothingMatches_SaysSoInsteadOfTheEmptyLibraryMessage()
    {
        _library.GetClipsAsync().Returns(TestData.Clips(TestData.Clip("Kick")));
        LibraryViewModel viewModel = Create();
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.SearchText = "zzz";

        Assert.Empty(viewModel.Clips);
        Assert.Equal("No clips match your search.", viewModel.EmptyMessage);
    }

    [Fact]
    public async Task Search_Cleared_ListsEverythingAgain()
    {
        AudioClip kick = TestData.Clip("Kick");
        AudioClip snare = TestData.Clip("Snare");
        _library.GetClipsAsync().Returns(TestData.Clips(kick, snare));
        LibraryViewModel viewModel = Create();
        await viewModel.LoadCommand.ExecuteAsync(null);
        viewModel.SearchText = "kick";

        viewModel.SearchText = string.Empty;

        Assert.Equal([kick, snare], viewModel.Clips);
    }

    [Fact]
    public async Task Sort_ByNameAndLongest_ReordersTheList()
    {
        AudioClip zed = TestData.Clip("Zed");
        AudioClip amp = TestData.Clip("Amp");
        zed.Duration = TimeSpan.FromSeconds(9);
        amp.Duration = TimeSpan.FromSeconds(2);
        _library.GetClipsAsync().Returns(TestData.Clips(zed, amp));
        LibraryViewModel viewModel = Create();
        await viewModel.LoadCommand.ExecuteAsync(null);

        viewModel.SortOrder = LibrarySortOrder.Name;
        Assert.Equal([amp, zed], viewModel.Clips);

        viewModel.SortOrder = LibrarySortOrder.Longest;
        Assert.Equal([zed, amp], viewModel.Clips);
    }

    [Fact]
    public async Task Export_ShowsItsOwnProgressNotThePullToRefreshSpinner()
    {
        TaskCompletionSource<string?> gate = new();
        _library.ExportClipAsync(default!, default).ReturnsForAnyArgs(gate.Task);
        LibraryViewModel viewModel = Create();

        Task export = viewModel.ExportClipAsync(TestData.Clip("Beat"), AudioExportFormat.Aac);

        Assert.True(viewModel.IsExporting);
        Assert.False(viewModel.IsBusy);
        gate.SetResult("Music/Beat.m4a");
        await export;
        Assert.False(viewModel.IsExporting);
    }

    [Fact]
    public async Task Load_WhileAlreadyLoading_DoesNotStartASecondLoad()
    {
        TaskCompletionSource<IReadOnlyList<AudioClip>> gate = new();
        _library.GetClipsAsync().Returns(gate.Task);
        LibraryViewModel viewModel = Create();

        Task first = viewModel.LoadCommand.ExecuteAsync(null);
        await viewModel.LoadCommand.ExecuteAsync(null);
        gate.SetResult(TestData.Clips());
        await first;

        await _library.Received(1).GetClipsAsync();
    }

    [Fact]
    public async Task Play_LoadsTheClipPlaysItThenShowsThePlayer()
    {
        AudioClip clip = TestData.Clip("Song");

        await Create().PlayCommand.ExecuteAsync(clip);

        Received.InOrder(
        () =>
        {
            _playback.LoadAsync(clip);
            _playback.Play();
            _navigation.GoToAsync("//player");
        });
    }

    [Fact]
    public async Task Play_NoClip_DoesNothing()
    {
        await Create().PlayCommand.ExecuteAsync(null);

        _playback.DidNotReceive().Play();
        await _navigation.DidNotReceiveWithAnyArgs().GoToAsync(default!);
    }

    [Fact]
    public void SetViewMode_ChangesTheLayoutAndRemembersIt()
    {
        LibraryViewModel viewModel = Create();

        viewModel.SetViewModeCommand.Execute(LibraryViewMode.Tile);

        Assert.Equal(LibraryViewMode.Tile, viewModel.ViewMode);
        Assert.Equal("Tile", _preferences.Get("library_view_mode", string.Empty));
    }

    [Fact]
    public void SetViewMode_ThenANewViewModel_StartsInThatMode()
    {
        Create().SetViewModeCommand.Execute(LibraryViewMode.List);

        Assert.Equal(LibraryViewMode.List, Create().ViewMode);
    }
    #endregion
}
