using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

public partial class PlayerViewModel : BaseViewModel, IDisposable
{
    private readonly IAudioPlaybackService _playbackService;
    private IDispatcherTimer? _positionTimer;

    [ObservableProperty]
    public partial string ClipName { get; set; } = "Nothing loaded";

    [ObservableProperty]
    public partial bool IsPlaying { get; set; }

    [ObservableProperty]
    public partial double PositionSeconds { get; set; }

    [ObservableProperty]
    public partial double DurationSeconds { get; set; } = 1;

    [ObservableProperty]
    public partial double Volume { get; set; } = 1.0;

    public string PositionText => TimeSpan.FromSeconds(PositionSeconds).ToString(@"mm\:ss");

    public string DurationText => TimeSpan.FromSeconds(DurationSeconds).ToString(@"mm\:ss");

    public PlayerViewModel(IAudioPlaybackService playbackService)
    {
        _playbackService = playbackService;
        Title = "Player";

        _playbackService.PlaybackStateChanged += OnPlaybackStateChanged;
        RefreshFromService();
    }

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

    public void StopTicking() => _positionTimer?.Stop();

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

    [RelayCommand]
    private void Stop() => _playbackService.Stop();

    [RelayCommand]
    private void Seek(double positionSeconds) => _playbackService.Seek(TimeSpan.FromSeconds(positionSeconds));

    partial void OnVolumeChanged(double value) => _playbackService.Volume = value;

    private void OnPlaybackStateChanged(object? sender, EventArgs e) => RefreshFromService();

    private void RefreshFromService()
    {
        ClipName = _playbackService.CurrentClip?.Name ?? "Nothing loaded";
        IsPlaying = _playbackService.IsPlaying;
        PositionSeconds = _playbackService.Position.TotalSeconds;
        DurationSeconds = Math.Max(_playbackService.Duration.TotalSeconds, 1);
        OnPropertyChanged(nameof(PositionText));
        OnPropertyChanged(nameof(DurationText));
    }

    public void Dispose()
    {
        _playbackService.PlaybackStateChanged -= OnPlaybackStateChanged;
        StopTicking();
    }
}
