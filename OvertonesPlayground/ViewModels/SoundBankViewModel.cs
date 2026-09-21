using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Models;
using OvertonesPlayground.Ontology.Concepts;
using OvertonesPlayground.Ontology.Facets;
using OvertonesPlayground.Ontology.Model;
using OvertonesPlayground.Ontology.Querying;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

///<summary>
///View model for the Sound Bank: a browser over the ~2,200 bundled samples, driven by the sample ontology. It searches by
///text, filters by instrument (drilling down the taxonomy), kit, content type, tempo, key, sound character, stereo image,
///length, loudness and envelope, sorts by any acoustic facet, finds sounds that sound like a chosen one, auditions a sound, and
///copies one into the user's library.
///</summary>
public partial class SoundBankViewModel : BaseViewModel
{
    #region Constants
    ///<summary>
    ///Voice key the audition voice plays under, well away from the Launchpad's pad and track keys.
    ///</summary>
    internal const int PreviewVoiceKey = -9001;

    private const int MaxKitChips = 16;
    private const string UpKey = "^up";

    ///<summary>
    ///The tempo ranges offered as filters. They cover the whole range without overlapping, so a sound is in exactly one band.
    ///The edges sit half a BPM below the whole numbers in the labels because rows show a tempo rounded to a whole number: a
    ///detected 139.9 is listed as "140 BPM", so it belongs under 140-159 and not under 120-139.
    ///</summary>
    private static readonly TempoBand[] _tempoBands =
    [
        new("slow", "Under 80", 0, 79.5),
        new("80", "80-99", 79.5, 99.5),
        new("100", "100-119", 99.5, 119.5),
        new("120", "120-139", 119.5, 139.5),
        new("140", "140-159", 139.5, 159.5),
        new("160", "160+", 159.5, double.PositiveInfinity),
    ];
    #endregion

    #region Fields
    private readonly IAudioLibraryService _libraryService;
    private readonly ISampleAssetStore _assets;
    private readonly ISampleCatalogService _catalog;
    private readonly IFileSystem _fileSystem;
    private readonly IAudioPlaybackService _playback;
    private readonly ISamplePickerService _picker;

    private FacetGroupViewModel? _instrumentGroup;
    private IReadOnlyDictionary<string, int> _instrumentCounts = new Dictionary<string, int>();
    private InstrumentConcept? _instrumentSelection;
    private SampleIndex? _index;
    private CancellationTokenSource? _previewCts;
    private SampleRowViewModel? _previewRow;
    private Dictionary<string, SampleRowViewModel> _rows = [];
    private Sample? _similarTo;
    #endregion

    #region Constructors
    public SoundBankViewModel(
        ISampleCatalogService catalog,
        ISampleAssetStore assets,
        IAudioPlaybackService playback,
        IAudioLibraryService libraryService,
        IFileSystem fileSystem,
        ISamplePickerService picker,
        ILogger<SoundBankViewModel> logger) : base(logger)
    {
        _catalog = catalog;
        _assets = assets;
        _playback = playback;
        _libraryService = libraryService;
        _fileSystem = fileSystem;
        _picker = picker;
        _picker.PickingChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsPickMode));
            OnPropertyChanged(nameof(PickPrompt));
        };
        Title = "Sound Bank";
    }
    #endregion

    #region Private methods
    private IEnumerable<SampleSpecification> AllSpecifications()
    {
        if (_instrumentSelection is not null)
        {
            yield return SampleSpecs.InstrumentIs(_instrumentSelection.Key);
        }

        foreach (FacetGroupViewModel group in FacetGroups.Where(group => !ReferenceEquals(group, _instrumentGroup)))
        {
            foreach (SampleSpecification specification in group.Specifications())
            {
                yield return specification;
            }
        }
    }

    private void BuildFacetGroups(SampleIndex index)
    {
        _instrumentCounts = index.CountByInstrument();
        _instrumentGroup = new FacetGroupViewModel("Instrument", [], key => SampleSpecs.InstrumentIs(key));
        RebuildInstrumentChips();

        FacetGroupViewModel kits = new(
            "Kit",
            [.. index.GetKits().Take(MaxKitChips).Select(kit => new FacetChipViewModel(kit.Concept.Key, kit.Name, kit.Members.Count))],
            key => SampleSpecs.KitIs(key));

        FacetGroupViewModel types = new(
            "Type",
            EnumChips(index.CountBy(sample => (ContentType?)sample.Classification.ContentType.Value), [ContentType.OneShot, ContentType.Loop, ContentType.Break, ContentType.Phrase, ContentType.Stab, ContentType.Texture, ContentType.Transition]),
            key => SampleSpecs.ContentTypeIs(Enum.Parse<ContentType>(key)));

        FacetGroupViewModel tempo = new(
            "Tempo (BPM)",
            [.. _tempoBands.Select(band => new FacetChipViewModel(band.Key, band.Label, index.All.Count(band.Specification.IsSatisfiedBy)))
                .Where(chip => chip.Count > 0)],
            key => _tempoBands.Single(band => band.Key == key).Specification);

        FacetGroupViewModel musicalKey = new(
            "Key",
            [.. Enumerable.Range(0, 12)
                .Select(pitchClass => new FacetChipViewModel(pitchClass.ToString(CultureInfo.InvariantCulture), MusicalNotes.PitchClassName(pitchClass), index.All.Count(sample => sample.EffectivePitchClass == pitchClass)))
                .Where(chip => chip.Count > 0)],
            key => SampleSpecs.PitchClassIs(int.Parse(key, CultureInfo.InvariantCulture)));

        TonalCharacter[] characters = [TonalCharacter.Pitched, TonalCharacter.Unpitched, TonalCharacter.Noisy, TonalCharacter.Harmonic, TonalCharacter.Metallic, TonalCharacter.Sub, TonalCharacter.Dark, TonalCharacter.Warm, TonalCharacter.Bright];
        FacetGroupViewModel character = new(
            "Character",
            [.. characters.Select(flag => new FacetChipViewModel(flag.ToString(), flag.ToString(), index.All.Count(sample => sample.Tonality?.Character.HasFlag(flag) == true)))],
            key => SampleSpecs.HasCharacter(Enum.Parse<TonalCharacter>(key)),
            isMultiSelect: true);

        FacetGroupViewModel stereo = new(
            "Stereo",
            EnumChips(index.CountBy(sample => sample.Stereo?.Image), Enum.GetValues<StereoImage>()),
            key => SampleSpecs.StereoIs(Enum.Parse<StereoImage>(key)));

        FacetGroupViewModel length = new(
            "Length",
            EnumChips(index.CountBy(sample => sample.Technical?.Length), Enum.GetValues<LengthClass>()),
            key => SampleSpecs.Length(Enum.Parse<LengthClass>(key)));

        FacetGroupViewModel loudness = new(
            "Loudness",
            EnumChips(index.CountBy(sample => sample.Dynamics?.Loudness), Enum.GetValues<LoudnessClass>()),
            key => SampleSpecs.Loudness(Enum.Parse<LoudnessClass>(key)));

        FacetGroupViewModel envelope = new(
            "Envelope",
            EnumChips(index.CountBy(sample => sample.Dynamics?.Shape), Enum.GetValues<EnvelopeShape>()),
            key => SampleSpecs.EnvelopeIs(Enum.Parse<EnvelopeShape>(key)));

        int flagged = index.All.Count(sample => sample.Classification.NeedsReview);
        FacetGroupViewModel review = new(
            "Curation",
            [new FacetChipViewModel("review", "Needs review", flagged)],
            _ => SampleSpecs.NeedsReview());

        FacetGroups = [_instrumentGroup, kits, types, tempo, musicalKey, character, stereo, length, loudness, envelope, review];
    }

    ///<summary>
    ///Chips for the values of an enum that actually occur, in the order given.
    ///</summary>
    private static List<FacetChipViewModel> EnumChips<T>(IReadOnlyDictionary<T, int> counts, IEnumerable<T> order)
        where T : struct, Enum =>
        [.. order.Where(value => counts.GetValueOrDefault(value) > 0).Select(value => new FacetChipViewModel(value.ToString(), SampleDetailViewModel.Words(value.ToString()), counts[value]))];

    private bool HasAnyFilter() =>
        !string.IsNullOrWhiteSpace(SearchText) || _instrumentSelection is not null || _similarTo is not null || FacetGroups.Any(group => !ReferenceEquals(group, _instrumentGroup) && group.HasSelection);

    ///<summary>
    ///Runs the current search, filters and sort and replaces the result list in one go, so the list is rebuilt once rather
    ///than once per item.
    ///</summary>
    private void Refresh()
    {
        if (_index is null)
        {
            return;
        }

        long started = Stopwatch.GetTimestamp();
        SampleSpecification? filter = null;
        foreach (SampleSpecification specification in AllSpecifications())
        {
            filter = filter is null ? specification : filter & specification;
        }

        IReadOnlyList<Sample> found = _index.Query(new SampleQuery
        {
            Text = SearchText,
            Filter = filter,
            Sort = SortKey,
            Descending = SortDescending,
            SimilarTo = _similarTo,
        });

        Results = [.. found.Select(sample => _rows[sample.Id])];
        ResultSummary = $"{Results.Count:N0} of {_index.Count:N0} sounds";
        HasActiveFilters = HasAnyFilter();
        OnPropertyChanged(nameof(IsSimilarMode));
        Log_Refreshed(Results.Count, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    ///<summary>
    ///Shows the instrument concepts one level below the current selection, plus a way back up.
    ///</summary>
    private void RebuildInstrumentChips()
    {
        Taxonomy<InstrumentConcept> taxonomy = Taxonomies.Instruments;
        InstrumentConcept parent = _instrumentSelection ?? taxonomy.Root;
        List<FacetChipViewModel> chips = [];
        if (_instrumentSelection is not null)
        {
            InstrumentConcept? above = _instrumentSelection.Parent as InstrumentConcept;
            bool aboveIsRoot = above is null || above.Parent is null;
            chips.Add(new FacetChipViewModel(UpKey, $"‹ Back to {(aboveIsRoot ? "all instruments" : above!.DisplayName)}", -1));
        }

        foreach (InstrumentConcept child in taxonomy.ChildrenOf(parent).Where(child => _instrumentCounts.GetValueOrDefault(child.Key) > 0 && child.Key != "unclassified"))
        {
            chips.Add(new FacetChipViewModel(child.Key, child.DisplayName, _instrumentCounts[child.Key]));
        }

        if (_instrumentGroup is not null)
        {
            _instrumentGroup.Chips = chips;
            _instrumentGroup.Subtitle = _instrumentSelection is null ? "all" : $"{_instrumentSelection.Path}  ({_instrumentCounts.GetValueOrDefault(_instrumentSelection.Key):N0})";
        }
    }

    private void ToggleInstrument(FacetChipViewModel chip)
    {
        if (chip.Key == UpKey)
        {
            _instrumentSelection = _instrumentSelection?.Parent is InstrumentConcept { Parent: not null } above ? above : null;
        }
        else
        {
            _instrumentSelection = Taxonomies.Instruments.Get(chip.Key);
        }

        RebuildInstrumentChips();
    }

    partial void OnSearchTextChanged(string value) => Refresh();

    partial void OnSortDescendingChanged(bool value) => Refresh();

    partial void OnSortKeyChanged(SampleSortKey value) => Refresh();

    [LoggerMessage(Level = LogLevel.Information, Message = "Sound Bank loaded {Count} sounds in {Milliseconds} ms.")]
    private partial void Log_Loaded(int count, double milliseconds);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sound Bank query matched {Count} sounds in {Milliseconds:0.0} ms.")]
    private partial void Log_Refreshed(int count, double milliseconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "Sound Bank could not be loaded.")]
    private partial void Log_LoadFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not audition {Sample}.")]
    private partial void Log_PreviewFailed(string sample, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not add {Sample} to the library.")]
    private partial void Log_AddFailed(string sample, Exception ex);

    private static void SetPreviewing(SampleRowViewModel? row, bool isPreviewing)
    {
        if (row is not null)
        {
            row.IsPreviewing = isPreviewing;
        }
    }
    #endregion

    #region Properties
    ///<summary>
    ///What the result list says when it has no rows: still loading, failed to load, or no sound matches.
    ///</summary>
    public string EmptyMessage => _index is null
        ? IsBusy ? "Loading the sound bank..." : "The sound bank couldn't be loaded. Leave this page and come back to try again."
        : "No sounds match. Try removing a filter.";

    ///<summary>
    ///True while another page has asked the user to choose a sound: the page then offers "Use this sound" and "Cancel".
    ///</summary>
    public bool IsPickMode => _picker.IsPicking;

    ///<summary>
    ///What the sound is being chosen for, shown above the list in pick mode.
    ///</summary>
    public string? PickPrompt => _picker.Prompt;
    #endregion

    #region Commands
    ///<summary>
    ///Copies the sound into the library, like <see cref="AddToLibraryCommand"/>, and hands it to the page that asked for one.
    ///</summary>
    [RelayCommand]
    private async Task UseSampleAsync(SampleRowViewModel? row)
    {
        row ??= SelectedRow;
        if (row is null || !_picker.IsPicking)
        {
            return;
        }

        try
        {
            string destination = Path.Combine(_fileSystem.AppDataDirectory, "Clips");
            string path = await _assets.CopyToAsync(row.Sample.Id, destination);
            AudioClip clip = await _libraryService.AddClipAsync(path, row.Name);
            await _picker.CompleteAsync(clip);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or InvalidOperationException)
        {
            Log_AddFailed(row.Name, ex);
            StatusMessage = "Couldn't use that sound.";
        }
    }

    ///<summary>
    ///Cancels choosing a sound and goes back to the page that asked.
    ///</summary>
    [RelayCommand]
    private Task CancelPickAsync() => _picker.CancelAsync();

    ///<summary>
    ///Ends any pick still waiting, because the user has left the Sound Bank without choosing.
    ///</summary>
    public void AbandonPick() => _picker.Abandon();

    ///<summary>
    ///Copies a sound into the user's library, so the editor, player, mixer and launchpad can use it like any imported clip.
    ///</summary>
    [RelayCommand]
    private async Task AddToLibraryAsync(SampleRowViewModel? row)
    {
        row ??= SelectedRow;
        if (row is null)
        {
            return;
        }

        try
        {
            string destination = Path.Combine(_fileSystem.AppDataDirectory, "Clips");
            string path = await _assets.CopyToAsync(row.Sample.Id, destination);
            AudioClip clip = await _libraryService.AddClipAsync(path, row.Name);
            StatusMessage = $"Added \"{clip.Name}\" to your library.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or InvalidOperationException)
        {
            Log_AddFailed(row.Name, ex);
            StatusMessage = "Couldn't add that sound to your library.";
        }
    }

    ///<summary>
    ///Removes the search text, every filter and any "sounds like" selection.
    ///</summary>
    [RelayCommand]
    private void ClearFilters()
    {
        _instrumentSelection = null;
        _similarTo = null;
        OnPropertyChanged(nameof(IsSimilarMode));
        SimilarToName = null;
        foreach (FacetGroupViewModel group in FacetGroups.Where(group => !ReferenceEquals(group, _instrumentGroup)))
        {
            group.Clear();
        }

        RebuildInstrumentChips();
        if (string.IsNullOrEmpty(SearchText))
        {
            Refresh();
        }
        else
        {
            SearchText = string.Empty;
        }
    }

    ///<summary>
    ///Leaves "sounds like" mode and goes back to the normal sort order.
    ///</summary>
    [RelayCommand]
    private void ClearSimilar()
    {
        _similarTo = null;
        SimilarToName = null;
        OnPropertyChanged(nameof(IsSimilarMode));
        Refresh();
    }

    ///<summary>
    ///Lists the sounds nearest to <paramref name="row"/> (or the selected sound) in acoustic feature space, nearest first.
    ///</summary>
    [RelayCommand]
    private void FindSimilar(SampleRowViewModel? row)
    {
        row ??= SelectedRow;
        if (row is null)
        {
            return;
        }

        _similarTo = row.Sample;
        SimilarToName = row.Name;
        OnPropertyChanged(nameof(IsSimilarMode));
        Refresh();
    }

    ///<summary>
    ///Loads the catalog the first time the page appears. Later calls do nothing.
    ///</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_index is not null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = null;
        OnPropertyChanged(nameof(EmptyMessage));
        try
        {
            _index = await _catalog.GetIndexAsync();
            _rows = _index.All.ToDictionary(sample => sample.Id, sample => new SampleRowViewModel(sample), StringComparer.Ordinal);
            BuildFacetGroups(_index);
            Refresh();
            Log_Loaded(_index.Count, _catalog.LoadDuration.TotalMilliseconds);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or FileNotFoundException)
        {
            Log_LoadFailed(ex);
            StatusMessage = "The sound bank couldn't be loaded.";
        }
        finally
        {
            IsBusy = false;
            OnPropertyChanged(nameof(EmptyMessage));
        }
    }

    ///<summary>
    ///Shows a sound that was suggested as related to the selected one.
    ///</summary>
    [RelayCommand]
    private void ShowRelated(RelatedSampleViewModel? related)
    {
        if (related is not null && _rows.TryGetValue(related.Sample.Id, out SampleRowViewModel? row))
        {
            SelectRow(row);
        }
    }

    ///<summary>
    ///Makes <paramref name="row"/> the sound shown in the detail panel.
    ///</summary>
    [RelayCommand]
    private void SelectRow(SampleRowViewModel? row)
    {
        if (row is null || _index is null)
        {
            return;
        }

        if (SelectedRow is not null)
        {
            SelectedRow.IsSelected = false;
        }

        row.IsSelected = true;
        SelectedRow = row;
        Detail = new SampleDetailViewModel(row.Sample, _index);
    }

    ///<summary>
    ///Stops the audition, if one is playing.
    ///</summary>
    [RelayCommand]
    private void StopPreview()
    {
        _previewCts?.Cancel();
        _playback.StopPad(PreviewVoiceKey);
        SetPreviewing(_previewRow, false);
        _previewRow = null;
    }

    ///<summary>
    ///Turns a chip on or off and re-runs the search. In the instrument row a chip drills down the taxonomy (or, for the
    ///"back" chip, up).
    ///</summary>
    [RelayCommand]
    private void ToggleChip(FacetChipViewModel? chip)
    {
        if (chip is null)
        {
            return;
        }

        FacetGroupViewModel? group = FacetGroups.FirstOrDefault(candidate => candidate.Chips.Contains(chip));
        if (ReferenceEquals(group, _instrumentGroup))
        {
            ToggleInstrument(chip);
        }
        else
        {
            group?.Toggle(chip);
        }

        Refresh();
    }

    ///<summary>
    ///Reverses the sort order.
    ///</summary>
    [RelayCommand]
    private void ToggleSortDirection() => SortDescending = !SortDescending;

    ///<summary>
    ///Auditions a sound (copying it out of the app package the first time), or stops it if it is the one already playing.
    ///</summary>
    [RelayCommand]
    private async Task TogglePreviewAsync(SampleRowViewModel? row)
    {
        row ??= SelectedRow;
        if (row is null)
        {
            return;
        }

        bool wasThisOne = ReferenceEquals(_previewRow, row);
        StopPreview();
        if (wasThisOne)
        {
            return;
        }

        CancellationTokenSource cts = new();
        _previewCts = cts;
        try
        {
            string path = await _assets.GetLocalPathAsync(row.Sample.Id, cts.Token);
            if (cts.IsCancellationRequested)
            {
                return;
            }

            _previewRow = row;
            row.IsPreviewing = true;
            // Named arguments select the record's own constructor, whose defaults are speed 1 and no pan. `new PadVoiceOptions()`
            // would be the struct default instead: volume 0 and speed 0, which the player rejects as unsupported.
            _playback.TriggerVoice(PreviewVoiceKey, path, new PadVoiceOptions(Volume: 1.0));

            // The voice ends by itself; keep the button in step with it.
            double seconds = row.Sample.Technical?.DurationSeconds ?? 1.0;
            await Task.Delay(TimeSpan.FromSeconds(seconds), cts.Token);
            if (ReferenceEquals(_previewRow, row))
            {
                row.IsPreviewing = false;
                _previewRow = null;
            }
        }
        catch (OperationCanceledException)
        {
            // Stopped, or replaced by another audition: nothing to report.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            Log_PreviewFailed(row.Name, ex);
            SetPreviewing(row, false);
            _previewRow = null;
            StatusMessage = "Couldn't play that sound.";
        }
    }
    #endregion

    #region Public properties
    ///<summary>
    ///The detail panel's content for the selected sound.
    ///</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetail))]
    public partial SampleDetailViewModel? Detail { get; set; }

    ///<summary>
    ///Whether a sound is selected, so the detail panel has something to show.
    ///</summary>
    public bool HasDetail => Detail is not null;

    ///<summary>
    ///Every filter row: instrument, kit, type, tempo, key, character, stereo, length, loudness, envelope and curation.
    ///</summary>
    [ObservableProperty]
    public partial IReadOnlyList<FacetGroupViewModel> FacetGroups { get; set; } = [];

    ///<summary>
    ///Whether the search text, a filter or "sounds like" is narrowing the list.
    ///</summary>
    [ObservableProperty]
    public partial bool HasActiveFilters { get; set; }

    ///<summary>
    ///Whether the filter rows are shown.
    ///</summary>
    [ObservableProperty]
    public partial bool IsFilterPanelVisible { get; set; } = true;

    ///<summary>
    ///Whether the list is ordered by similarity to one sound rather than by the chosen sort.
    ///</summary>
    public bool IsSimilarMode => _similarTo is not null;

    ///<summary>
    ///The sounds matching the current search and filters, in the chosen order. Replaced as a whole on every change.
    ///</summary>
    [ObservableProperty]
    public partial IReadOnlyList<SampleRowViewModel> Results { get; set; } = [];

    ///<summary>
    ///"312 of 2,220 sounds".
    ///</summary>
    [ObservableProperty]
    public partial string ResultSummary { get; set; } = string.Empty;

    ///<summary>
    ///Free text that must match the name, instrument, kit, style or key of a result.
    ///</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    ///<summary>
    ///The row shown in the detail panel.
    ///</summary>
    [ObservableProperty]
    public partial SampleRowViewModel? SelectedRow { get; set; }

    ///<summary>
    ///Name of the sound the list is ordered by similarity to, when in that mode.
    ///</summary>
    [ObservableProperty]
    public partial string? SimilarToName { get; set; }

    ///<summary>
    ///Reverse the sort order.
    ///</summary>
    [ObservableProperty]
    public partial bool SortDescending { get; set; }

    ///<summary>
    ///What the list is sorted by.
    ///</summary>
    [ObservableProperty]
    public partial SampleSortKey SortKey { get; set; } = SampleSortKey.Name;

    ///<summary>
    ///The sort orders to choose from.
    ///</summary>
    public IReadOnlyList<SampleSortKey> SortKeys { get; } = Enum.GetValues<SampleSortKey>();
    #endregion

    #region Nested types
    ///<summary>
    ///One tempo filter: a label and the half-open range of BPM it stands for.
    ///</summary>
    private sealed record TempoBand(string Key, string Label, double From, double Below)
    {
        public SampleSpecification Specification { get; } = SampleSpecs.TempoBand(From, Below);
    }
    #endregion
}
