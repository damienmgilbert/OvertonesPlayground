using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

public partial class MixerChannelViewModel : ObservableObject
{
    private readonly IAudioPlaybackService _playbackService;

    public MixerChannelStrip Channel { get; }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial double Volume { get; set; }

    [ObservableProperty]
    public partial double Pan { get; set; }

    [ObservableProperty]
    public partial bool IsMuted { get; set; }

    [ObservableProperty]
    public partial bool IsSoloed { get; set; }

    [ObservableProperty]
    public partial string SourceLabel { get; set; } = "No sample loaded";

    public MixerChannelViewModel(MixerChannelStrip channel, IAudioPlaybackService playbackService)
    {
        Channel = channel;
        _playbackService = playbackService;
        Name = channel.Name;
        Volume = channel.Volume;
        Pan = channel.Pan;
        IsMuted = channel.IsMuted;
        IsSoloed = channel.IsSoloed;
    }

    public void AssignSource(string filePath, string label)
    {
        Channel.SourceClipPath = filePath;
        SourceLabel = label;
        _playbackService.PlayChannel(Channel);
    }

    partial void OnVolumeChanged(double value)
    {
        Channel.Volume = value;
        _playbackService.UpdateChannel(Channel);
    }

    partial void OnPanChanged(double value)
    {
        Channel.Pan = value;
        _playbackService.UpdateChannel(Channel);
    }

    partial void OnIsMutedChanged(bool value)
    {
        Channel.IsMuted = value;
        _playbackService.UpdateChannel(Channel);
    }

    partial void OnIsSoloedChanged(bool value) => Channel.IsSoloed = value;

    public void Stop() => _playbackService.StopChannel(Channel.Id);

    [RelayCommand]
    private void ToggleMute() => IsMuted = !IsMuted;

    [RelayCommand]
    private void ToggleSolo() => IsSoloed = !IsSoloed;
}
