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
            RulerStepSeconds = PickRulerStep(DurationSeconds);
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

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to save the trimmed clip from '{ClipName}'.")]
    private partial void Log_SaveFailed(Exception exception, string clipName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Saved trimmed clip '{ClipName}'.")]
    private partial void Log_SavedTrim(string clipName);

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
    }

    partial void OnTrimStartSecondsChanged(double value)
    {
        OnPropertyChanged(nameof(StartHandleX));
        OnPropertyChanged(nameof(StartTimeText));
    }

    partial void OnViewportWidthChanged(double value) => RaiseGeometryChanged();

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
    ///Re-raises every property derived from <see cref="ViewportWidth"/>/<see cref="DurationSeconds"/>.
    ///</summary>
    private void RaiseGeometryChanged()
    {
        OnPropertyChanged(nameof(PixelsPerSecond));
        OnPropertyChanged(nameof(StartHandleX));
        OnPropertyChanged(nameof(EndHandleX));
        OnPropertyChanged(nameof(TotalTimeText));
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

            string outputPath = Mode == TrimMode.TrimMiddle ? await _editorService.CutAsync(LoadedClip.FilePath, start, end, baseName) : await _editorService.TrimAsync(LoadedClip.FilePath, start, end, baseName);

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
    ///Horizontal offset, in device-independent pixels, of the end handle within the waveform view.
    ///</summary>
    public double EndHandleX => TrimEndSeconds * PixelsPerSecond;

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
    ///How many device-independent pixels represent one second of audio in the waveform view.
    ///</summary>
    public double PixelsPerSecond => DurationSeconds > 0 ? ViewportWidth / DurationSeconds : 0;

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
    ///Horizontal offset, in device-independent pixels, of the start handle within the waveform view.
    ///</summary>
    public double StartHandleX => TrimStartSeconds * PixelsPerSecond;

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
    ///Normalized amplitude peaks (0 to 1) for the loaded clip's waveform.
    ///</summary>
    [ObservableProperty]
    public partial float[] WaveformPeaks { get; set; } = [];
    #endregion
}
