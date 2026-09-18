using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

///<summary>
///View model for the visual Trim page: a waveform with draggable handles for selecting a range, either kept (Trim) or
///removed (Trim Middle), plus quick volume/fade adjustments applied at Save time.
///</summary>
[QueryProperty(nameof(ClipId), "clipId")]
public partial class TrimViewModel : BaseViewModel
{
    #region Constants

    ///<summary>
    ///How far each tap of a start/end stepper button moves that handle.
    ///</summary>
    private const double NudgeStepSeconds = 0.1;
    #endregion

    #region Fields
    ///<summary>
    ///How far, in each direction, to search for a zero crossing when a handle drag completes.
    ///</summary>
    private static readonly TimeSpan ZeroCrossingSearchWindow = TimeSpan.FromSeconds(0.01);
    ///<summary>
    ///The zoom levels the Zoom In/Out commands step through.
    ///</summary>
    private static readonly double[] ZoomSteps = [1, 2, 4, 8, 16];
    private readonly IAudioEditorService _editorService;
    private readonly IAudioLibraryService _libraryService;
    private readonly IAudioPlaybackService _playbackService;
    private IDispatcherTimer? _positionTimer;
    private readonly Stack<(double Start, double End)> _redoStack = new();
    private readonly Stack<(double Start, double End)> _undoStack = new();
    #endregion

    #region Constructors
    public TrimViewModel(IAudioEditorService editorService, IAudioLibraryService libraryService, IAudioPlaybackService playbackService, ILogger<TrimViewModel> logger) : base(logger)
    {
        _editorService = editorService;
        _libraryService = libraryService;
        _playbackService = playbackService;
        Title = "Trim";
    }
    #endregion

    #region Private methods
    private bool CanRedo() => _redoStack.Count > 0;

    private bool CanUndo() => _undoStack.Count > 0;

    [RelayCommand]
    private void DecreaseEnd() { TrimEndSeconds = Math.Clamp(TrimEndSeconds - NudgeStepSeconds, TrimStartSeconds, DurationSeconds); }
    [RelayCommand]
    private void DecreaseStart() { TrimStartSeconds = Math.Clamp(TrimStartSeconds - NudgeStepSeconds, 0, TrimEndSeconds); }

    ///<summary>
    ///Shifts the zoom window so it keeps including <paramref name="focusSeconds"/>, e.g. while dragging a handle near
    ///the edge of a zoomed-in view.
    ///</summary>
    private void FollowWindowIfNeeded(double focusSeconds)
    {
        if (!IsZoomed)
        {
            return;
        }

        double margin = VisibleSeconds * 0.1;
        double maxWindowStart = Math.Max(0, DurationSeconds - VisibleSeconds);

        if (focusSeconds < WindowStartSeconds + margin)
        {
            WindowStartSeconds = Math.Clamp(focusSeconds - margin, 0, maxWindowStart);
        }
        else if (focusSeconds > WindowStartSeconds + VisibleSeconds - margin)
        {
            WindowStartSeconds = Math.Clamp(focusSeconds - VisibleSeconds + margin, 0, maxWindowStart);
        }
    }

    ///<summary>
    ///Formats a seconds value as "mm:ss.f", matching the stepper/total time labels.
    ///</summary>
    private static string FormatTime(double totalSeconds)
    {
        totalSeconds = Math.Max(0, totalSeconds);
        int minutes = (int)(totalSeconds / 60);
        double seconds = totalSeconds - (minutes * 60);
        return $"{minutes:00}:{seconds:00.0}";
    }

    [RelayCommand]
    private void IncreaseEnd() { TrimEndSeconds = Math.Clamp(TrimEndSeconds + NudgeStepSeconds, TrimStartSeconds, DurationSeconds); }
    [RelayCommand]
    private void IncreaseStart() { TrimStartSeconds = Math.Clamp(TrimStartSeconds + NudgeStepSeconds, 0, TrimEndSeconds); }

    private async Task LoadClipAsync(string clipId)
    {
        IsBusy = true;
        try
        {
            IReadOnlyList<AudioClip> clips = await _libraryService.GetClipsAsync();
            AudioClip? clip = clips.FirstOrDefault(c => c.Id == clipId);
            if (clip is null)
            {
                Log_ClipNotFound(clipId);
                StatusMessage = "Could not find that clip.";
                return;
            }

            Log_LoadingClip(clip.Name, clipId);
            LoadedClip = clip;
            DurationSeconds = Math.Max(clip.Duration.TotalSeconds, 0.1);
            TrimStartSeconds = 0;
            TrimEndSeconds = DurationSeconds;
            ZoomLevel = 1;
            WindowStartSeconds = 0;
            _undoStack.Clear();
            _redoStack.Clear();
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();

            WaveformPeaks = await _editorService.GetWaveformPeaksAsync(clip.FilePath, 400);
            await _playbackService.LoadAsync(clip);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            Log_LoadClipFailed(ex, clipId);
            StatusMessage = "Couldn't load that clip.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Clip {ClipId} not found.")]
    private partial void Log_ClipNotFound(string clipId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load clip {ClipId} into the trim editor.")]
    private partial void Log_LoadClipFailed(Exception exception, string clipId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading clip '{ClipName}' ({ClipId}) into the trim editor.")]
    private partial void Log_LoadingClip(string clipName, string clipId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Saved trimmed clip '{ClipName}'.")]
    private partial void Log_SavedTrim(string clipName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to save the trimmed clip from '{ClipName}'.")]
    private partial void Log_SaveFailed(Exception exception, string clipName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to snap a handle to a zero crossing for clip '{ClipName}'.")]
    private partial void Log_SnapFailed(Exception exception, string clipName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Split clip '{ClipName}' at {PositionSeconds}s.")]
    private partial void Log_Split(string clipName, double positionSeconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to split clip '{ClipName}'.")]
    private partial void Log_SplitFailed(Exception exception, string clipName);

    partial void OnClipIdChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _ = LoadClipAsync(value);
        }
    }

    partial void OnDurationSecondsChanged(double value) => RaiseGeometryChanged();

    partial void OnTrimEndSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(EndHandleX));
        OnPropertyChanged(nameof(EndTimeText));
        FollowWindowIfNeeded(value);
    }

    partial void OnTrimStartSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(StartHandleX));
        OnPropertyChanged(nameof(StartTimeText));
        FollowWindowIfNeeded(value);
    }

    partial void OnViewportWidthChanged(double value) => RaiseGeometryChanged();

    partial void OnWindowStartSecondsChanged(double value) => RaiseGeometryChanged();

    partial void OnZoomLevelChanged(double value) => RaiseGeometryChanged();

    [RelayCommand]
    private void PanEarlier()
    {
        double maxWindowStart = Math.Max(0, DurationSeconds - VisibleSeconds);
        WindowStartSeconds = Math.Clamp(WindowStartSeconds - (VisibleSeconds * 0.5), 0, maxWindowStart);
    }

    [RelayCommand]
    private void PanLater()
    {
        double maxWindowStart = Math.Max(0, DurationSeconds - VisibleSeconds);
        WindowStartSeconds = Math.Clamp(WindowStartSeconds + (VisibleSeconds * 0.5), 0, maxWindowStart);
    }

    ///<summary>
    ///Chooses a "nice" ruler tick spacing (in seconds) that yields roughly 6-10 ticks across the clip.
    ///</summary>
    private static double PickRulerStep(double durationSeconds)
    {
        double[] niceSteps = [1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800];
        foreach (double step in niceSteps)
        {
            bool fitsWell = durationSeconds / step <= 10;
            if (fitsWell)
            {
                return step;
            }
        }

        return niceSteps[^1];
    }

    [RelayCommand]
    private void PlayPause()
    {
        if (_playbackService.IsPlaying)
        {
            _playbackService.Pause();
        }
        else
        {
            _playbackService.Play();
        }
    }

    ///<summary>
    ///Re-raises every property derived from <see cref="ViewportWidth"/>/<see cref="DurationSeconds"/>/ ///<see
    ///cref="ZoomLevel"/>/<see cref="WindowStartSeconds"/>, and recomputes the ruler spacing for the now-visible window.
    ///
    ///</summary>
    private void RaiseGeometryChanged()
    {
        RulerStepSeconds = PickRulerStep(VisibleSeconds);
        OnPropertyChanged(nameof(VisibleSeconds));
        OnPropertyChanged(nameof(PixelsPerSecond));
        OnPropertyChanged(nameof(StartHandleX));
        OnPropertyChanged(nameof(EndHandleX));
        OnPropertyChanged(nameof(TotalTimeText));
        OnPropertyChanged(nameof(IsZoomed));
        OnPropertyChanged(nameof(ZoomLevelText));
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (_redoStack.Count == 0)
        {
            return;
        }

        _undoStack.Push((TrimStartSeconds, TrimEndSeconds));
        (double start, double end) = _redoStack.Pop();
        TrimStartSeconds = start;
        TrimEndSeconds = end;
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (LoadedClip is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            TimeSpan start = TimeSpan.FromSeconds(TrimStartSeconds);
            TimeSpan end = TimeSpan.FromSeconds(TrimEndSeconds);
            string baseName = $"{LoadedClip.Name} (trimmed)";

            string outputPath;
            if (Mode == TrimMode.TrimMiddle)
            {
                outputPath = await _editorService.CutAsync(LoadedClip.FilePath, start, end, baseName);
            }
            else
            {
                outputPath = await _editorService.TrimAsync(LoadedClip.FilePath, start, end, baseName);
            }

            bool hasFade = FadeInSeconds > 0 || FadeOutSeconds > 0;
            if (hasFade)
            {
                outputPath = await _editorService.ApplyFadeAsync(outputPath, TimeSpan.FromSeconds(FadeInSeconds), TimeSpan.FromSeconds(FadeOutSeconds), baseName);
            }

            bool hasGain = Math.Abs(GainDb) > 0.01;
            if (hasGain)
            {
                outputPath = await _editorService.ApplyGainAsync(outputPath, GainDb, baseName);
            }

            AudioClip saved = await _libraryService.AddClipAsync(outputPath, baseName, isUserRecording: true);
            Log_SavedTrim(saved.Name);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            Log_SaveFailed(ex, LoadedClip.Name);
            StatusMessage = "Couldn't save the trimmed clip.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SetMode(TrimMode mode) { Mode = mode; }
    [RelayCommand]
    private void SkipToEnd() { _playbackService.Seek(TimeSpan.FromSeconds(TrimEndSeconds)); }
    [RelayCommand]
    private void SkipToStart() { _playbackService.Seek(TimeSpan.FromSeconds(TrimStartSeconds)); }
    [RelayCommand]
    private async Task SplitAtPlayheadAsync()
    {
        bool cannotSplit = LoadedClip is null || IsBusy || PositionSeconds <= 0 || PositionSeconds >= DurationSeconds;
        if (cannotSplit)
        {
            StatusMessage = "Move the playhead into the clip first.";
            return;
        }

        IsBusy = true;
        try
        {
            TimeSpan at = TimeSpan.FromSeconds(PositionSeconds);
            string baseName = LoadedClip!.Name;
            (string beforePath, string afterPath) = await _editorService.SplitAsync(LoadedClip.FilePath, at, $"{baseName} (part 1)", $"{baseName} (part 2)");

            await _libraryService.AddClipAsync(beforePath, $"{baseName} (part 1)", isUserRecording: true);
            await _libraryService.AddClipAsync(afterPath, $"{baseName} (part 2)", isUserRecording: true);

            Log_Split(baseName, PositionSeconds);
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            Log_SplitFailed(ex, LoadedClip!.Name);
            StatusMessage = "Couldn't split that clip.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ToggleTool(TrimTool tool) { ActiveTool = ActiveTool == tool ? TrimTool.None : tool; }
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (_undoStack.Count == 0)
        {
            return;
        }

        _redoStack.Push((TrimStartSeconds, TrimEndSeconds));
        (double start, double end) = _undoStack.Pop();
        TrimStartSeconds = start;
        TrimEndSeconds = end;
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ZoomIn()
    {
        double next = ZoomSteps.FirstOrDefault(z => z > ZoomLevel, ZoomSteps[^1]);
        ZoomLevel = next;
        WindowStartSeconds = Math.Clamp(WindowStartSeconds, 0, Math.Max(0, DurationSeconds - VisibleSeconds));
    }

    [RelayCommand]
    private void ZoomOut()
    {
        double next = ZoomSteps.LastOrDefault(z => z < ZoomLevel, ZoomSteps[0]);
        ZoomLevel = next;
        WindowStartSeconds = Math.Clamp(WindowStartSeconds, 0, Math.Max(0, DurationSeconds - VisibleSeconds));
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Snapshots the current selection onto the undo stack and clears redo history. Called once at the start of a handle
    ///drag, so the whole drag undoes as a single step.
    ///</summary>
    public void BeginHandleDrag()
    {
        _undoStack.Push((TrimStartSeconds, TrimEndSeconds));
        _redoStack.Clear();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    ///<summary>
    ///Moves the preview playhead to the given position, in seconds.
    ///</summary>
    public void SeekToPosition(double seconds)
    {
        double clamped = Math.Clamp(seconds, 0, DurationSeconds);
        _playbackService.Seek(TimeSpan.FromSeconds(clamped));
        PositionSeconds = clamped;
    }

    ///<summary>
    ///Sets the end handle to an exact time, in seconds, clamped to [start, duration]. Used by tap-to-type entry.
    ///</summary>
    public void SetEndTime(double seconds) => TrimEndSeconds = Math.Clamp(seconds, TrimStartSeconds, DurationSeconds);

    ///<summary>
    ///Sets the start handle to an exact time, in seconds, clamped to [0, end]. Used by tap-to-type entry.
    ///</summary>
    public void SetStartTime(double seconds) => TrimStartSeconds = Math.Clamp(seconds, 0, TrimEndSeconds);

    ///<summary>
    ///Snaps the end handle to the nearest zero crossing, if one is found nearby. Called after a drag completes so a cut
    ///doesn't land mid-waveform and click audibly. Leaves the handle where it was dropped if snapping fails.
    ///</summary>
    public async Task SnapEndToZeroCrossingAsync()
    {
        if (LoadedClip is null)
        {
            return;
        }

        try
        {
            TimeSpan snapped = await _editorService.FindNearestZeroCrossingAsync(LoadedClip.FilePath, TimeSpan.FromSeconds(TrimEndSeconds), ZeroCrossingSearchWindow);
            TrimEndSeconds = Math.Clamp(snapped.TotalSeconds, TrimStartSeconds, DurationSeconds);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            Log_SnapFailed(ex, LoadedClip.Name);
        }
    }

    ///<summary>
    ///Snaps the start handle to the nearest zero crossing, if one is found nearby. Called after a drag completes so a
    ///cut doesn't land mid-waveform and click audibly. Leaves the handle where it was dropped if snapping fails.
    ///</summary>
    public async Task SnapStartToZeroCrossingAsync()
    {
        if (LoadedClip is null)
        {
            return;
        }

        try
        {
            TimeSpan snapped = await _editorService.FindNearestZeroCrossingAsync(LoadedClip.FilePath, TimeSpan.FromSeconds(TrimStartSeconds), ZeroCrossingSearchWindow);
            TrimStartSeconds = Math.Clamp(snapped.TotalSeconds, 0, TrimEndSeconds);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            Log_SnapFailed(ex, LoadedClip.Name);
        }
    }

    ///<summary>
    ///Starts a periodic timer that refreshes <see cref="PositionSeconds"/> while the page is visible.
    ///</summary>
    public void StartTicking(IDispatcher dispatcher)
    {
        if (_positionTimer is not null)
        {
            return;
        }

        _positionTimer = dispatcher.CreateTimer();
        _positionTimer.Interval = TimeSpan.FromMilliseconds(100);
        _positionTimer.Tick += (_, _) =>
        {
            PositionSeconds = _playbackService.Position.TotalSeconds;
            IsPlaying = _playbackService.IsPlaying;
        };
        _positionTimer.Start();
    }

    ///<summary>
    ///Stops the position-refresh timer, e.g. when the page is no longer visible.
    ///</summary>
    public void StopTicking() => _positionTimer?.Stop();
    #endregion

    #region Public properties
    ///<summary>
    ///Which quick-action slider, if any, the bottom bar currently has revealed.
    ///</summary>
    [ObservableProperty]
    public partial TrimTool ActiveTool { get; set; }

    ///<summary>
    ///Id of the clip to load (set via query property when navigating to the page).
    ///</summary>
    [ObservableProperty]
    public partial string? ClipId { get; set; }

    ///<summary>
    ///Total duration of the loaded clip, in seconds.
    ///</summary>
    [ObservableProperty]
    public partial double DurationSeconds { get; set; } = 1;

    ///<summary>
    ///Horizontal offset, in device-independent pixels, of the end handle within the current zoom window.
    ///</summary>
    public double EndHandleX => (TrimEndSeconds - WindowStartSeconds) * PixelsPerSecond;

    ///<summary>
    ///The end handle's position, formatted as "mm:ss.f".
    ///</summary>
    public string EndTimeText => FormatTime(TrimEndSeconds);

    ///<summary>
    ///Fade-in duration to apply at Save time, in seconds. Zero applies no fade-in.
    ///</summary>
    [ObservableProperty]
    public partial double FadeInSeconds { get; set; }

    ///<summary>
    ///Fade-out duration to apply at Save time, in seconds. Zero applies no fade-out.
    ///</summary>
    [ObservableProperty]
    public partial double FadeOutSeconds { get; set; }

    ///<summary>
    ///Gain to apply at Save time, in decibels. Zero leaves volume unchanged.
    ///</summary>
    [ObservableProperty]
    public partial double GainDb { get; set; }

    ///<summary>
    ///Whether the shared transport is currently previewing this clip.
    ///</summary>
    [ObservableProperty]
    public partial bool IsPlaying { get; set; }

    ///<summary>
    ///Whether the waveform is zoomed in past 1x.
    ///</summary>
    public bool IsZoomed => ZoomLevel > 1.01;

    ///<summary>
    ///The clip currently loaded into the trim editor.
    ///</summary>
    [ObservableProperty]
    public partial AudioClip? LoadedClip { get; set; }

    ///<summary>
    ///Whether Save keeps the selection (<see cref="TrimMode.Trim"/>) or removes it (<see cref="TrimMode.TrimMiddle"/>).
    ///</summary>
    [ObservableProperty]
    public partial TrimMode Mode { get; set; }

    ///<summary>
    ///How many device-independent pixels represent one second of audio in the current zoom window.
    ///</summary>
    public double PixelsPerSecond => VisibleSeconds > 0 ? ViewportWidth / VisibleSeconds : 0;

    ///<summary>
    ///Current preview playback position, in seconds.
    ///</summary>
    [ObservableProperty]
    public partial double PositionSeconds { get; set; }

    ///<summary>
    ///Spacing, in seconds, between the waveform ruler's tick marks.
    ///</summary>
    [ObservableProperty]
    public partial double RulerStepSeconds { get; set; } = 30;

    ///<summary>
    ///Horizontal offset, in device-independent pixels, of the start handle within the current zoom window.
    ///</summary>
    public double StartHandleX => (TrimStartSeconds - WindowStartSeconds) * PixelsPerSecond;

    ///<summary>
    ///The start handle's position, formatted as "mm:ss.f".
    ///</summary>
    public string StartTimeText => FormatTime(TrimStartSeconds);

    ///<summary>
    ///The clip's total duration, formatted as "mm:ss.f".
    ///</summary>
    public string TotalTimeText => FormatTime(DurationSeconds);

    ///<summary>
    ///End of the selected range, in seconds.
    ///</summary>
    [ObservableProperty]
    public partial double TrimEndSeconds { get; set; }

    ///<summary>
    ///Start of the selected range, in seconds.
    ///</summary>
    [ObservableProperty]
    public partial double TrimStartSeconds { get; set; }

    ///<summary>
    ///Rendered width, in device-independent pixels, of the waveform view - set from the page once its container is
    ///measured.
    ///</summary>
    [ObservableProperty]
    public partial double ViewportWidth { get; set; } = 360;

    ///<summary>
    ///How many seconds of the clip are currently visible across the waveform view's width, given the current zoom.
    ///</summary>
    public double VisibleSeconds => ZoomLevel > 0 ? DurationSeconds / ZoomLevel : DurationSeconds;

    ///<summary>
    ///Normalized amplitude peaks (0 to 1) for the loaded clip's waveform.
    ///</summary>
    [ObservableProperty]
    public partial float[] WaveformPeaks { get; set; } = [];

    ///<summary>
    ///Clip-relative time, in seconds, at the left edge of the current zoom window.
    ///</summary>
    [ObservableProperty]
    public partial double WindowStartSeconds { get; set; }

    ///<summary>
    ///How many times zoomed in the waveform view currently is. 1 shows the whole clip.
    ///</summary>
    [ObservableProperty]
    public partial double ZoomLevel { get; set; } = 1;

    /// <summary>
    /// The current zoom level formatted as a string, e.g., "1x", "2x", "4x".
    /// </summary>
    public string ZoomLevelText => $"{ZoomLevel:0.#}x";
    #endregion
}
