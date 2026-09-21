using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

///<summary>
///View model for the global audio player. Exposes playback controls, current clip metadata and position information
///used by the player UI.
///</summary>
public partial class PlayerViewModel : BaseViewModel, IDisposable
{
    #region Fields
    private readonly INavigationService _navigation;
    private readonly IAudioPlaybackService _playbackService;
    private IDispatcherTimer? _positionTimer;
    private bool _ticking;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the view model and syncs its initial state from the shared playback service.
    ///</summary>
    public PlayerViewModel(IAudioPlaybackService playbackService, INavigationService navigation, ILogger<PlayerViewModel> logger) : base(logger)
    {
        _playbackService = playbackService;
        _navigation = navigation;
        Title = "Player";

        Volume = _playbackService.Volume;
        _playbackService.PlaybackStateChanged += OnPlaybackStateChanged;
        RefreshFromService();
    }
    #endregion

    #region Private methods
    [LoggerMessage(Level = LogLevel.Debug, Message = "Disposing.")]
    private partial void Log_Disposing();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Pausing playback.")]
    private partial void Log_PausingPlayback();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Seeking to {PositionSeconds}s.")]
    private partial void Log_Seeking(double positionSeconds);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting playback.")]
    private partial void Log_StartingPlayback();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Stopping playback.")]
    private partial void Log_StoppingPlayback();

    private void OnPlaybackStateChanged(object? sender, EventArgs e) => RefreshFromService();

    partial void OnVolumeChanged(double value) => _playbackService.Volume = value;

    ///<summary>
    ///Pauses if currently playing, otherwise resumes/starts playback.
    ///</summary>
    [RelayCommand(CanExecute = nameof(HasClip))]
    private void PlayPause()
    {
        if (_playbackService.IsPlaying)
        {
            Log_PausingPlayback();
            _playbackService.Pause();
        }
        else
        {
            Log_StartingPlayback();
            _playbackService.Play();
        }
    }

    ///<summary>
    ///Pulls the latest clip name, playing state, and position/duration from the playback service.
    ///</summary>
    private void RefreshFromService()
    {
        ClipName = _playbackService.CurrentClip?.Name ?? "Nothing loaded";
        HasClip = _playbackService.CurrentClip is not null;
        IsPlaying = _playbackService.IsPlaying;
        DurationSeconds = Math.Max(_playbackService.Duration.TotalSeconds, 1);

        // While the user drags the seek slider its own value is the position; the timer must not pull it back.
        if (!IsSeeking)
        {
            PositionSeconds = _playbackService.Position.TotalSeconds;
        }

        OnPropertyChanged(nameof(PositionText));
        OnPropertyChanged(nameof(DurationText));
        UpdateTimer();
    }

    ///<summary>
    ///Runs the position timer only while a clip is playing, so a paused or idle player isn't waking up five times a second.
    ///</summary>
    private void UpdateTimer()
    {
        if (!_ticking || _positionTimer is null)
        {
            return;
        }

        if (IsPlaying && !_positionTimer.IsRunning)
        {
            _positionTimer.Start();
        }
        else if (!IsPlaying && _positionTimer.IsRunning)
        {
            _positionTimer.Stop();
        }
    }

    ///<summary>
    ///Formats a length of time as mm:ss, or h:mm:ss once it reaches an hour.
    ///</summary>
    private static string FormatTime(double seconds)
    {
        TimeSpan time = TimeSpan.FromSeconds(seconds);
        return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"mm\:ss");
    }

    ///<summary>
    ///Opens the Library so a clip can be picked to play.
    ///</summary>
    [RelayCommand]
    private Task PickFromLibraryAsync() => _navigation.GoToAsync("//library");

    ///<summary>
    ///The user has grabbed the seek slider: stop the timer overwriting its value until it's released.
    ///</summary>
    public void BeginSeek() => IsSeeking = true;

    ///<summary>
    ///Shows the position under the seek slider's thumb while it's dragged.
    ///</summary>
    public void PreviewSeek(double positionSeconds)
    {
        if (IsSeeking)
        {
            PositionSeconds = positionSeconds;
            OnPropertyChanged(nameof(PositionText));
        }
    }

    ///<summary>
    ///The user let go of the seek slider: jump to where it was dropped and let the timer drive it again.
    ///</summary>
    public void EndSeek(double positionSeconds)
    {
        IsSeeking = false;
        SeekCommand.Execute(positionSeconds);
    }

    ///<summary>
    ///Moves playback to the given position, in seconds.
    ///</summary>
    [RelayCommand]
    private void Seek(double positionSeconds)
    {
        Log_Seeking(positionSeconds);
        _playbackService.Seek(TimeSpan.FromSeconds(positionSeconds));
    }

    ///<summary>
    ///Stops playback and resets position to the start.
    ///</summary>
    [RelayCommand(CanExecute = nameof(HasClip))]
    private void Stop()
    {
        Log_StoppingPlayback();
        _playbackService.Stop();
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Unsubscribes from the playback service and stops the position timer.
    ///</summary>
    public void Dispose()
    {
        Log_Disposing();
        _playbackService.PlaybackStateChanged -= OnPlaybackStateChanged;
        StopTicking();
    }

    ///<summary>
    ///Starts a periodic timer that refreshes the displayed position while the page is visible.
    ///</summary>
    public void StartTicking(IDispatcher dispatcher)
    {
        if (_positionTimer is not null)
        {
            _ticking = true;
            RefreshFromService();
            return;
        }

        _positionTimer = dispatcher.CreateTimer();
        _positionTimer.Interval = TimeSpan.FromMilliseconds(200);
        _positionTimer.Tick += (_, _) => RefreshFromService();
        _ticking = true;
        _positionTimer.Start();
    }

    ///<summary>
    ///Stops the position-refresh timer, e.g. when the page is no longer visible.
    ///</summary>
    public void StopTicking()
    {
        _ticking = false;
        _positionTimer?.Stop();
    }
    #endregion

    #region Public properties
    ///<summary>
    ///Name of the currently loaded clip or a placeholder when none is loaded.
    ///</summary>
    [ObservableProperty]
    public partial string ClipName { get; set; } = "Nothing loaded";

    ///<summary>
    ///Total duration of the loaded clip in seconds.
    ///</summary>
    [ObservableProperty]
    public partial double DurationSeconds { get; set; } = 1;

    ///<summary>
    ///Total duration formatted as mm:ss or h:mm:ss.
    ///</summary>
    public string DurationText => FormatTime(DurationSeconds);

    ///<summary>
    ///Whether a clip is loaded. Play and Stop do nothing without one.
    ///</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    public partial bool HasClip { get; set; }

    ///<summary>
    ///True while the user is dragging the seek slider.
    ///</summary>
    [ObservableProperty]
    public partial bool IsSeeking { get; set; }

    ///<summary>
    ///Whether playback is currently active.
    ///</summary>
    [ObservableProperty]
    public partial bool IsPlaying { get; set; }

    ///<summary>
    ///Current playback position in seconds.
    ///</summary>
    [ObservableProperty]
    public partial double PositionSeconds { get; set; }

    ///<summary>
    ///Current position formatted as mm:ss or h:mm:ss.
    ///</summary>
    public string PositionText => FormatTime(PositionSeconds);

    ///<summary>
    ///Master playback volume for the player.
    ///</summary>
    [ObservableProperty]
    public partial double Volume { get; set; } = 1.0;
    #endregion
}
