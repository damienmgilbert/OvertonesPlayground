using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

/// <summary>
/// View model for the global audio player. Exposes playback controls, current
/// clip metadata and position information used by the player UI.
/// </summary>
public partial class PlayerViewModel : BaseViewModel, IDisposable
{
    private readonly IAudioPlaybackService _playbackService;
    private IDispatcherTimer? _positionTimer;

    /// <summary>Name of the currently loaded clip or a placeholder when none is loaded.</summary>
    [ObservableProperty]
    public partial string ClipName { get; set; } = "Nothing loaded";

    /// <summary>Whether playback is currently active.</summary>
    [ObservableProperty]
    public partial bool IsPlaying { get; set; }

    /// <summary>Current playback position in seconds.</summary>
    [ObservableProperty]
    public partial double PositionSeconds { get; set; }

    /// <summary>Total duration of the loaded clip in seconds.</summary>
    [ObservableProperty]
    public partial double DurationSeconds { get; set; } = 1;

    /// <summary>Master playback volume for the player.</summary>
    [ObservableProperty]
    public partial double Volume { get; set; } = 1.0;

    /// <summary>Current position formatted as mm:ss.</summary>
    public string PositionText => TimeSpan.FromSeconds(PositionSeconds).ToString(@"mm\:ss");

    /// <summary>Total duration formatted as mm:ss.</summary>
    public string DurationText => TimeSpan.FromSeconds(DurationSeconds).ToString(@"mm\:ss");

    /// <summary>Creates the view model and syncs its initial state from the shared playback service.</summary>
    public PlayerViewModel(IAudioPlaybackService playbackService)
    {
        _playbackService = playbackService;
        Title = "Player";

        _playbackService.PlaybackStateChanged += OnPlaybackStateChanged;
        RefreshFromService();
    }

    /// <summary>Starts a periodic timer that refreshes the displayed position while the page is visible.</summary>
    public void StartTicking(IDispatcher dispatcher)
    {
        if (_positionTimer is not null)
        {
            return;
        }

        _positionTimer = dispatcher.CreateTimer();
        _positionTimer.Interval = TimeSpan.FromMilliseconds(200);
        _positionTimer.Tick += (_, _) => RefreshFromService();
        _positionTimer.Start();
    }

    /// <summary>Stops the position-refresh timer, e.g. when the page is no longer visible.</summary>
    public void StopTicking() => _positionTimer?.Stop();

    /// <summary>Pauses if currently playing, otherwise resumes/starts playback.</summary>
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

    /// <summary>Stops playback and resets position to the start.</summary>
    [RelayCommand]
    private void Stop() => _playbackService.Stop();

    /// <summary>Moves playback to the given position, in seconds.</summary>
    [RelayCommand]
    private void Seek(double positionSeconds) => _playbackService.Seek(TimeSpan.FromSeconds(positionSeconds));

    partial void OnVolumeChanged(double value) => _playbackService.Volume = value;

    private void OnPlaybackStateChanged(object? sender, EventArgs e) => RefreshFromService();

    /// <summary>Pulls the latest clip name, playing state, and position/duration from the playback service.</summary>
    private void RefreshFromService()
    {
        ClipName = _playbackService.CurrentClip?.Name ?? "Nothing loaded";
        IsPlaying = _playbackService.IsPlaying;
        PositionSeconds = _playbackService.Position.TotalSeconds;
        DurationSeconds = Math.Max(_playbackService.Duration.TotalSeconds, 1);
        OnPropertyChanged(nameof(PositionText));
        OnPropertyChanged(nameof(DurationText));
    }

    /// <summary>Unsubscribes from the playback service and stops the position timer.</summary>
    public void Dispose()
    {
        _playbackService.PlaybackStateChanged -= OnPlaybackStateChanged;
        StopTicking();
    }
}
