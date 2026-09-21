namespace OvertonesPlayground.Tests.ViewModels;

public sealed class SoundBankViewModelTests : IDisposable
{
    #region Fields
    private readonly ISampleAssetStore _assets = Substitute.For<ISampleAssetStore>();
    private readonly ISampleCatalogService _catalog = Substitute.For<ISampleCatalogService>();
    private readonly TempFileSystem _fileSystem = new();
    private readonly IAudioLibraryService _library = Substitute.For<IAudioLibraryService>();
    private readonly IAudioPlaybackService _playback = Substitute.For<IAudioPlaybackService>();
    #endregion

    #region Constructors
    public SoundBankViewModelTests()
    {
        _catalog.GetIndexAsync(Arg.Any<CancellationToken>()).Returns(new SampleIndex(TestSamples.Library()));
        _assets.GetLocalPathAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call => Task.FromResult($"/cache/{call.Arg<string>()}"));
    }
    #endregion

    #region Private methods
    private static FacetChipViewModel Chip(SoundBankViewModel viewModel, string group, string key) => viewModel.FacetGroups.Single(g => g.Title == group).Chips.Single(c => c.Key == key);

    private SoundBankViewModel Create() => new(_catalog, _assets, _playback, _library, _fileSystem, Substitute.For<ISamplePickerService>(), NullLogger<SoundBankViewModel>.Instance);

    private async Task<SoundBankViewModel> LoadedAsync()
    {
        SoundBankViewModel viewModel = Create();
        await viewModel.LoadCommand.ExecuteAsync(null);
        return viewModel;
    }
    #endregion

    #region Public methods
    [Fact]
    public async Task AddToLibrary_CopiesTheSoundIntoAppStorageAndRegistersIt()
    {
        string copy = _fileSystem.InAppData("Clips", "Vinyl Dirt 1.wav");
        _assets.CopyToAsync("Vinyl Dirt 1.wav", _fileSystem.InAppData("Clips"), Arg.Any<CancellationToken>()).Returns(copy);
        _library.AddClipAsync(copy, "Vinyl Dirt 1").Returns(new AudioClip { Name = "Vinyl Dirt 1", FilePath = copy });
        SoundBankViewModel viewModel = await LoadedAsync();

        await viewModel.AddToLibraryCommand.ExecuteAsync(viewModel.Results.Single(r => r.Name == "Vinyl Dirt 1"));

        _ = await _library.Received(1).AddClipAsync(copy, "Vinyl Dirt 1");
        Assert.Equal("Added \"Vinyl Dirt 1\" to your library.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task AddToLibrary_ItCannotBeCopied_ReportsItAndAddsNothing()
    {
        _assets.CopyToAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns<Task<string>>(_ => throw new IOException("disk full"));
        SoundBankViewModel viewModel = await LoadedAsync();

        await viewModel.AddToLibraryCommand.ExecuteAsync(viewModel.Results[0]);

        Assert.Equal("Couldn't add that sound to your library.", viewModel.StatusMessage);
        _ = await _library.DidNotReceive().AddClipAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task AddToLibrary_NothingSelectedAndNoRow_DoesNothing()
    {
        SoundBankViewModel viewModel = await LoadedAsync();

        await viewModel.AddToLibraryCommand.ExecuteAsync(null);

        _ = await _assets.DidNotReceive().CopyToAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClearFilters_RemovesTheSearchEveryChipAndTheInstrumentDrillDown()
    {
        SoundBankViewModel viewModel = await LoadedAsync();
        viewModel.SearchText = "kick";
        viewModel.ToggleChipCommand.Execute(Chip(viewModel, "Kit", "909"));
        viewModel.ToggleChipCommand.Execute(Chip(viewModel, "Instrument", "percussion"));
        viewModel.ToggleChipCommand.Execute(Chip(viewModel, "Tempo (BPM)", "80"));
        viewModel.ToggleChipCommand.Execute(Chip(viewModel, "Key", "4"));

        viewModel.ClearFiltersCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Equal(25, viewModel.Results.Count);
        Assert.False(viewModel.HasActiveFilters);
        Assert.All(viewModel.FacetGroups, group => Assert.False(group.HasSelection));
        Assert.Equal("all", viewModel.FacetGroups[0].Subtitle);
    }

    public void Dispose() => _fileSystem.Dispose();

    [Fact]
    public async Task FindSimilar_OrdersByAcousticSimilarityAndCanBeDismissed()
    {
        SoundBankViewModel viewModel = await LoadedAsync();
        SampleRowViewModel kick = viewModel.Results.Single(r => r.Name == "Kick Test 1");

        viewModel.FindSimilarCommand.Execute(kick);

        Assert.True(viewModel.IsSimilarMode);
        Assert.Equal("Kick Test 1", viewModel.SimilarToName);
        Assert.DoesNotContain(viewModel.Results, r => r.Name == "Kick Test 1");
        Assert.StartsWith("Kick", viewModel.Results[0].Name, StringComparison.Ordinal);
        Assert.True(viewModel.HasActiveFilters);

        viewModel.ClearSimilarCommand.Execute(null);
        Assert.False(viewModel.IsSimilarMode);
        Assert.Null(viewModel.SimilarToName);
        Assert.Equal(25, viewModel.Results.Count);
    }

    [Fact]
    public async Task FindSimilar_WithoutARow_UsesTheSelectedSound()
    {
        SoundBankViewModel viewModel = await LoadedAsync();
        viewModel.SelectRowCommand.Execute(viewModel.Results.Single(r => r.Name == "Snare Test 1"));

        viewModel.FindSimilarCommand.Execute(null);

        Assert.Equal("Snare Test 1", viewModel.SimilarToName);
    }

    [Fact]
    public void HasDetail_IsFalseUntilASoundIsSelected() { Assert.False(Create().HasDetail); }
    [Fact]
    public async Task InstrumentRow_ChoosingACategoryFiltersAndShowsItsChildrenWithABackChip()
    {
        SoundBankViewModel viewModel = await LoadedAsync();

        viewModel.ToggleChipCommand.Execute(Chip(viewModel, "Instrument", "percussion"));

        Assert.Equal(21, viewModel.Results.Count);
        string[] keys = [.. viewModel.FacetGroups[0].Chips.Select(c => c.Key)];
        Assert.Equal("^up", keys[0]);
        Assert.Contains("kick", keys);
        Assert.Contains("snare", keys);
        Assert.Contains("hihat", keys);
        Assert.DoesNotContain("percussion", keys);
        Assert.Equal("‹ Back to all instruments", viewModel.FacetGroups[0].Chips[0].Label);
        Assert.StartsWith("Percussion", viewModel.FacetGroups[0].Subtitle, StringComparison.Ordinal);
        Assert.True(viewModel.HasActiveFilters);
    }

    [Fact]
    public async Task InstrumentRow_DrillingDownTwiceNarrowsFurtherAndBackClimbsOneLevel()
    {
        SoundBankViewModel viewModel = await LoadedAsync();

        viewModel.ToggleChipCommand.Execute(Chip(viewModel, "Instrument", "percussion"));
        viewModel.ToggleChipCommand.Execute(Chip(viewModel, "Instrument", "hihat"));

        Assert.Equal(6, viewModel.Results.Count);
        Assert.Equal("‹ Back to Percussion", viewModel.FacetGroups[0].Chips[0].Label);
        Assert.Contains("hihat-closed", viewModel.FacetGroups[0].Chips.Select(c => c.Key));
        Assert.Contains("Hi-Hat", viewModel.FacetGroups[0].Subtitle, StringComparison.Ordinal);

        viewModel.ToggleChipCommand.Execute(viewModel.FacetGroups[0].Chips[0]);
        Assert.Equal(21, viewModel.Results.Count);

        viewModel.ToggleChipCommand.Execute(viewModel.FacetGroups[0].Chips[0]);
        Assert.Equal(25, viewModel.Results.Count);
        Assert.Equal("all", viewModel.FacetGroups[0].Subtitle);
    }

    [Fact]
    public async Task InstrumentRow_StartsWithTheCategoriesThatHaveSounds()
    {
        SoundBankViewModel viewModel = await LoadedAsync();

        string[] keys = [.. viewModel.FacetGroups[0].Chips.Select(c => c.Key)];

        Assert.Contains("percussion", keys);
        Assert.Contains("bass", keys);
        Assert.Contains("loop-groove", keys);
        Assert.DoesNotContain("keys", keys);
        Assert.DoesNotContain("unclassified", keys);
        Assert.DoesNotContain("^up", keys);
        Assert.Equal(21, Chip(viewModel, "Instrument", "percussion").Count);
    }

    [Fact]
    public void IsFilterPanelVisible_StartsOn() { Assert.True(Create().IsFilterPanelVisible); }
    [Fact]
    public async Task KeyRow_OffersEachKeyThatOccursWithItsNoteNameAndCount()
    {
        SoundBankViewModel viewModel = await LoadedAsync();

        FacetGroupViewModel key = viewModel.FacetGroups.Single(g => g.Title == "Key");

        // The 808 is named in E; the six test kicks have a detected fundamental of 55 Hz, which is an A.
        Assert.Equal(["4", "9"], key.Chips.Select(c => c.Key));
        Assert.Equal(["E  1", "A  6"], key.Chips.Select(c => c.Display));
        Assert.False(key.IsMultiSelect);
    }

    [Fact]
    public async Task Load_BuildsAFilterRowForEveryFacet()
    {
        SoundBankViewModel viewModel = await LoadedAsync();

        Assert.Equal(["Instrument", "Kit", "Type", "Tempo (BPM)", "Key", "Character", "Stereo", "Length", "Loudness", "Envelope", "Curation"], viewModel.FacetGroups.Select(g => g.Title));
        Assert.True(viewModel.FacetGroups.Single(g => g.Title == "Character").IsMultiSelect);
        Assert.False(viewModel.FacetGroups.Single(g => g.Title == "Kit").IsMultiSelect);
    }

    [Fact]
    public async Task Load_CalledAgain_DoesNotReadTheCatalogAgain()
    {
        SoundBankViewModel viewModel = await LoadedAsync();

        await viewModel.LoadCommand.ExecuteAsync(null);

        _ = await _catalog.Received(1).GetIndexAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Load_CatalogCannotBeRead_ShowsAMessageInsteadOfThrowing()
    {
        _catalog.GetIndexAsync(Arg.Any<CancellationToken>()).Returns<Task<SampleIndex>>(_ => throw new InvalidDataException("missing"));
        SoundBankViewModel viewModel = Create();

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Equal("The sound bank couldn't be loaded.", viewModel.StatusMessage);
        Assert.Empty(viewModel.Results);
        Assert.False(viewModel.IsBusy);
        Assert.Contains("couldn't be loaded", viewModel.EmptyMessage);
    }

    [Fact]
    public async Task EmptyMessage_SaysLoadingWhileTheCatalogLoadsThenNoMatches()
    {
        TaskCompletionSource<SampleIndex> gate = new();
        _catalog.GetIndexAsync(Arg.Any<CancellationToken>()).Returns(gate.Task);
        SoundBankViewModel viewModel = Create();

        Task load = viewModel.LoadCommand.ExecuteAsync(null);
        Assert.Equal("Loading the sound bank...", viewModel.EmptyMessage);

        gate.SetResult(new SampleIndex(TestSamples.Library()));
        await load;
        Assert.Equal("No sounds match. Try removing a filter.", viewModel.EmptyMessage);
    }

    [Fact]
    public async Task Load_ChipsOnlyOfferValuesThatOccurAndShowTheirCounts()
    {
        SoundBankViewModel viewModel = await LoadedAsync();

        FacetChipViewModel nine = Chip(viewModel, "Kit", "909");
        Assert.Equal(3, nine.Count);
        Assert.Equal("Roland 909  3", nine.Display);
        Assert.DoesNotContain(viewModel.FacetGroups.Single(g => g.Title == "Stereo").Chips, c => c.Key == nameof(StereoImage.OutOfPhase));
        Assert.DoesNotContain(viewModel.FacetGroups.Single(g => g.Title == "Type").Chips, c => c.Key == nameof(ContentType.Phrase));
    }

    [Fact]
    public async Task Load_ShowsEverySoundAndTheirCount()
    {
        SoundBankViewModel viewModel = await LoadedAsync();

        Assert.Equal(25, viewModel.Results.Count);
        Assert.Equal("25 of 25 sounds", viewModel.ResultSummary);
        Assert.False(viewModel.IsBusy);
        Assert.Null(viewModel.StatusMessage);
        Assert.Equal("Sound Bank", viewModel.Title);
    }

    [Fact]
    public async Task ResultList_IsReplacedAsAWholeRatherThanChangedInPlace()
    {
        SoundBankViewModel viewModel = await LoadedAsync();
        IReadOnlyList<SampleRowViewModel> before = viewModel.Results;

        viewModel.SearchText = "kick";

        Assert.NotSame(before, viewModel.Results);
        Assert.Equal(25, before.Count);
    }

    [Fact]
    public async Task SearchText_NarrowsTheListAsTheUserTypes()
    {
        SoundBankViewModel viewModel = await LoadedAsync();

        viewModel.SearchText = "909";

        Assert.Equal(3, viewModel.Results.Count);
        Assert.Equal("3 of 25 sounds", viewModel.ResultSummary);
        Assert.True(viewModel.HasActiveFilters);
    }

    [Fact]
    public async Task SelectRow_ShowsTheDetailAndMarksOnlyThatRow()
    {
        SoundBankViewModel viewModel = await LoadedAsync();
        SampleRowViewModel first = viewModel.Results[0];
        SampleRowViewModel second = viewModel.Results[1];

        viewModel.SelectRowCommand.Execute(first);
        viewModel.SelectRowCommand.Execute(second);

        Assert.False(first.IsSelected);
        Assert.True(second.IsSelected);
        Assert.Same(second, viewModel.SelectedRow);
        Assert.Equal(second.Name, viewModel.Detail!.Title);
        Assert.True(viewModel.HasDetail);
    }

    [Fact]
    public async Task ShowRelated_SelectsTheRelatedSound()
    {
        SoundBankViewModel viewModel = await LoadedAsync();
        SampleRowViewModel kick = viewModel.Results.Single(r => r.Name == "Kick 909 DMX 1");
        viewModel.SelectRowCommand.Execute(kick);
        RelatedSampleViewModel variation = viewModel.Detail!.Related.Single(g => g.Title == "Variations").Items.Single();

        viewModel.ShowRelatedCommand.Execute(variation);

        Assert.Equal("Kick 909 DMX 2", viewModel.SelectedRow!.Name);
    }

    [Fact]
    public async Task SortKey_ReordersTheListAndSortDirectionReversesIt()
    {
        SoundBankViewModel viewModel = await LoadedAsync();

        viewModel.SortKey = SampleSortKey.Duration;
        viewModel.ToggleSortDirectionCommand.Execute(null);

        Assert.True(viewModel.SortDescending);
        Assert.Equal("Break Ghosts 90 bpm", viewModel.Results[0].Name);

        viewModel.ToggleSortDirectionCommand.Execute(null);
        Assert.Equal(0.12, viewModel.Results[0].Sample.Technical!.DurationSeconds);
    }

    [Fact]
    public async Task TempoRow_OffersOnlyTheBandsThatHaveSoundsInTempoOrder()
    {
        SoundBankViewModel viewModel = await LoadedAsync();

        FacetGroupViewModel tempo = viewModel.FacetGroups.Single(g => g.Title == "Tempo (BPM)");

        Assert.Equal(["80", "160"], tempo.Chips.Select(c => c.Key));
        Assert.Equal(["80-99  1", "160+  1"], tempo.Chips.Select(c => c.Display));
        Assert.False(tempo.IsMultiSelect);
    }

    [Fact]
    public async Task ToggleChip_FiltersInDifferentRowsCombine()
    {
        SoundBankViewModel viewModel = await LoadedAsync();

        viewModel.ToggleChipCommand.Execute(Chip(viewModel, "Kit", "909"));
        viewModel.ToggleChipCommand.Execute(Chip(viewModel, "Type", nameof(ContentType.OneShot)));
        viewModel.SearchText = "kick";

        Assert.Equal(["Kick 909 DMX 1", "Kick 909 DMX 2"], viewModel.Results.Select(r => r.Name));
    }

    [Fact]
    public async Task ToggleChip_Key_UsesTheNamedKeyOrElseTheDetectedPitch()
    {
        SoundBankViewModel viewModel = await LoadedAsync();

        viewModel.ToggleChipCommand.Execute(Chip(viewModel, "Key", "4"));
        Assert.Equal(["808 Oracle 1"], viewModel.Results.Select(r => r.Name));

        viewModel.ToggleChipCommand.Execute(Chip(viewModel, "Key", "9"));
        Assert.Equal(6, viewModel.Results.Count);
        Assert.All(viewModel.Results, row => Assert.StartsWith("Kick Test", row.Name, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ToggleChip_KitChip_FiltersToThatKitAndTurnsOffAgain()
    {
        SoundBankViewModel viewModel = await LoadedAsync();
        FacetChipViewModel kit = Chip(viewModel, "Kit", "909");

        viewModel.ToggleChipCommand.Execute(kit);
        Assert.True(kit.IsSelected);
        Assert.All(viewModel.Results, row => Assert.Equal("909", row.Sample.Classification.Kit?.Value));
        Assert.Equal(3, viewModel.Results.Count);

        viewModel.ToggleChipCommand.Execute(kit);
        Assert.False(kit.IsSelected);
        Assert.Equal(25, viewModel.Results.Count);
        Assert.False(viewModel.HasActiveFilters);
    }

    [Fact]
    public async Task ToggleChip_MultiSelectRow_RequiresEveryChosenCharacter()
    {
        SoundBankViewModel viewModel = await LoadedAsync();

        viewModel.ToggleChipCommand.Execute(Chip(viewModel, "Character", nameof(TonalCharacter.Pitched)));
        int pitched = viewModel.Results.Count;
        viewModel.ToggleChipCommand.Execute(Chip(viewModel, "Character", nameof(TonalCharacter.Sub)));

        Assert.True(pitched > 1);
        Assert.Equal(["808 Oracle 1"], viewModel.Results.Select(r => r.Name));
    }

    [Fact]
    public async Task ToggleChip_Null_DoesNothing()
    {
        SoundBankViewModel viewModel = await LoadedAsync();

        viewModel.ToggleChipCommand.Execute(null);

        Assert.Equal(25, viewModel.Results.Count);
    }

    [Fact]
    public async Task ToggleChip_SingleSelectRow_ReplacesTheChipThatWasOn()
    {
        SoundBankViewModel viewModel = await LoadedAsync();
        FacetChipViewModel mono = Chip(viewModel, "Stereo", nameof(StereoImage.Mono));
        FacetChipViewModel wide = Chip(viewModel, "Stereo", nameof(StereoImage.Wide));

        viewModel.ToggleChipCommand.Execute(mono);
        viewModel.ToggleChipCommand.Execute(wide);

        Assert.False(mono.IsSelected);
        Assert.True(wide.IsSelected);
        Assert.Equal(["Break Ghosts 90 bpm"], viewModel.Results.Select(r => r.Name));
    }

    [Fact]
    public async Task ToggleChip_TempoAndKeyCombineWithOtherRows()
    {
        SoundBankViewModel viewModel = await LoadedAsync();

        viewModel.ToggleChipCommand.Execute(Chip(viewModel, "Tempo (BPM)", "80"));
        viewModel.ToggleChipCommand.Execute(Chip(viewModel, "Key", "4"));

        Assert.Empty(viewModel.Results);
        Assert.Equal("0 of 25 sounds", viewModel.ResultSummary);
        Assert.True(viewModel.HasActiveFilters);
    }

    [Fact]
    public async Task ToggleChip_TempoBand_KeepsSoundsInThatRangeOnly()
    {
        SoundBankViewModel viewModel = await LoadedAsync();

        viewModel.ToggleChipCommand.Execute(Chip(viewModel, "Tempo (BPM)", "80"));
        Assert.Equal(["Break Ghosts 90 bpm"], viewModel.Results.Select(r => r.Name));

        viewModel.ToggleChipCommand.Execute(Chip(viewModel, "Tempo (BPM)", "160"));
        Assert.Equal(["Groove B 180 bpm"], viewModel.Results.Select(r => r.Name));
        Assert.False(Chip(viewModel, "Tempo (BPM)", "80").IsSelected);
    }

    [Fact]
    public async Task ToggleChip_TempoBand_MatchesTheWholeNumberTheRowShows()
    {
        // Regression from the tablet: a detected 139.9 BPM is shown as "140 BPM" but was listed under 120-139.
        _catalog.GetIndexAsync(Arg.Any<CancellationToken>()).Returns(new SampleIndex([TestSamples.Make("Rounds up", tempoBpm: 139.9), TestSamples.Make("Rounds down", tempoBpm: 139.4), TestSamples.Make("Lower edge", tempoBpm: 119.6),]));
        SoundBankViewModel viewModel = await LoadedAsync();

        viewModel.ToggleChipCommand.Execute(Chip(viewModel, "Tempo (BPM)", "140"));
        Assert.Equal(["Rounds up"], viewModel.Results.Select(r => r.Name));
        Assert.Contains("140 BPM", viewModel.Results[0].Badges);

        viewModel.ToggleChipCommand.Execute(Chip(viewModel, "Tempo (BPM)", "120"));
        Assert.Equal(["Lower edge", "Rounds down"], viewModel.Results.Select(r => r.Name));
        Assert.Contains("139 BPM", viewModel.Results.Single(r => r.Name == "Rounds down").Badges);
    }

    [Fact]
    public async Task TogglePreview_AnotherRow_ReplacesTheFirstAudition()
    {
        SoundBankViewModel viewModel = await LoadedAsync();
        SampleRowViewModel one = viewModel.Results.Single(r => r.Name == "Vinyl Dirt 1");
        SampleRowViewModel two = viewModel.Results.Single(r => r.Name == "Break Ghosts 90 bpm");
        Task first = viewModel.TogglePreviewCommand.ExecuteAsync(one);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Task second = viewModel.TogglePreviewCommand.ExecuteAsync(two);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.False(one.IsPreviewing);
        Assert.True(two.IsPreviewing);
        viewModel.StopPreviewCommand.Execute(null);
        await Task.WhenAll(first, second);
    }

    [Fact]
    public async Task TogglePreview_AssetCannotBeCopied_ReportsItAndLeavesNothingPlaying()
    {
        _assets.GetLocalPathAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns<Task<string>>(_ => throw new FileNotFoundException("gone"));
        SoundBankViewModel viewModel = await LoadedAsync();
        SampleRowViewModel row = viewModel.Results[0];

        await viewModel.TogglePreviewCommand.ExecuteAsync(row);

        Assert.Equal("Couldn't play that sound.", viewModel.StatusMessage);
        Assert.False(row.IsPreviewing);
        _playback.DidNotReceive().TriggerVoice(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<PadVoiceOptions>());
    }

    [Fact]
    public async Task TogglePreview_CopiesTheAssetOutAndStartsAVoice()
    {
        SoundBankViewModel viewModel = await LoadedAsync();
        SampleRowViewModel row = viewModel.Results.Single(r => r.Name == "Vinyl Dirt 1");

        Task preview = viewModel.TogglePreviewCommand.ExecuteAsync(row);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        _ = await _assets.Received(1).GetLocalPathAsync("Vinyl Dirt 1.wav", Arg.Any<CancellationToken>());
        _playback.Received(1).TriggerVoice(SoundBankViewModel.PreviewVoiceKey, "/cache/Vinyl Dirt 1.wav", Arg.Any<PadVoiceOptions>());
        Assert.True(row.IsPreviewing);

        viewModel.StopPreviewCommand.Execute(null);
        await preview;
        Assert.False(row.IsPreviewing);
    }

    [Fact]
    public async Task TogglePreview_PlaysAtFullVolumeAndNormalSpeed()
    {
        // Regression: `new PadVoiceOptions()` is the struct default (volume 0, speed 0); on the tablet a speed of 0 made the
        // player fail with MEDIA_ERROR_UNSUPPORTED and the sound was silent.
        SoundBankViewModel viewModel = await LoadedAsync();
        Task preview = viewModel.TogglePreviewCommand.ExecuteAsync(viewModel.Results.Single(r => r.Name == "Vinyl Dirt 1"));
        await Task.Delay(100, TestContext.Current.CancellationToken);

        _playback.Received(1).TriggerVoice(SoundBankViewModel.PreviewVoiceKey, Arg.Any<string>(), Arg.Is<PadVoiceOptions>(options => options.Volume == 1.0 && options.Speed == 1.0 && options.Balance == 0.0 && !options.Loop && options.MaxLength == null));

        viewModel.StopPreviewCommand.Execute(null);
        await preview;
    }

    [Fact]
    public async Task TogglePreview_TheSameRowAgain_StopsIt()
    {
        SoundBankViewModel viewModel = await LoadedAsync();
        SampleRowViewModel row = viewModel.Results.Single(r => r.Name == "Vinyl Dirt 1");
        Task first = viewModel.TogglePreviewCommand.ExecuteAsync(row);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        await viewModel.TogglePreviewCommand.ExecuteAsync(row);
        await first;

        Assert.False(row.IsPreviewing);
        _playback.Received().StopPad(SoundBankViewModel.PreviewVoiceKey);
        _playback.Received(1).TriggerVoice(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<PadVoiceOptions>());
    }

    [Fact]
    public async Task TogglePreview_WithoutARow_UsesTheSelectedSound()
    {
        SoundBankViewModel viewModel = await LoadedAsync();
        viewModel.SelectRowCommand.Execute(viewModel.Results.Single(r => r.Name == "Vinyl Dirt 1"));

        Task preview = viewModel.TogglePreviewCommand.ExecuteAsync(null);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        _playback.Received(1).TriggerVoice(SoundBankViewModel.PreviewVoiceKey, "/cache/Vinyl Dirt 1.wav", Arg.Any<PadVoiceOptions>());
        viewModel.StopPreviewCommand.Execute(null);
        await preview;
    }
    #endregion
}
