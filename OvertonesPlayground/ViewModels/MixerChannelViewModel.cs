using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

///<summary>
///View model for a single mixer channel. Wraps a <see cref="MixerChannelStrip"/> and exposes bindable properties and
///commands to control the channel.
///</summary>
public partial class MixerChannelViewModel : ObservableObject
{
    #region Fields
    private readonly IAudioPlaybackService _playbackService;
    #endregion

    #region Constructors
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
    #endregion

    #region Private methods
    partial void OnIsMutedChanged(bool value)
    {
        Channel.IsMuted = value;
        _playbackService.UpdateChannel(Channel);
    }

    partial void OnIsSoloedChanged(bool value) { Channel.IsSoloed = value; }

    partial void OnPanChanged(double value)
    {
        Channel.Pan = value;
        _playbackService.UpdateChannel(Channel);
    }

    partial void OnVolumeChanged(double value)
    {
        Channel.Volume = value;
        _playbackService.UpdateChannel(Channel);
    }

    [RelayCommand]
    private void ToggleMute() { IsMuted = !IsMuted; }
    [RelayCommand]
    private void TogglePlayback()
    {
        if(!HasSource)
        {
            return;
        }

        if(IsPlaying)
        {
            Stop();
        } else
        {
            _playbackService.PlayChannel(Channel);
            IsPlaying = true;
        }
    }

    [RelayCommand]
    private void ToggleSolo() { IsSoloed = !IsSoloed; }
    #endregion

    #region Public methods
    public void AssignSource(string filePath, string label)
    {
        // Loading a new sample shouldn't start it blaring - the user starts it with the toggle.
        if(IsPlaying)
        {
            Stop();
        }

        Channel.SourceClipPath = filePath;
        SourceLabel = label;
        OnPropertyChanged(nameof(HasSource));
    }

    public void Stop()
    {
        _playbackService.StopChannel(Channel.Id);
        IsPlaying = false;
    }
    #endregion

    #region Public properties
    ///<summary>
    ///Underlying model for this channel.
    ///</summary>
    public MixerChannelStrip Channel { get; }

    public bool HasSource => !string.IsNullOrEmpty(Channel.SourceClipPath);

    ///<summary>
    ///Whether the channel is muted.
    ///</summary>
    [ObservableProperty]
    public partial bool IsMuted { get; set; }

    ///<summary>
    ///Whether this channel's voice is currently sounding.
    ///</summary>
    [ObservableProperty]
    public partial bool IsPlaying { get; set; }

    ///<summary>
    ///Whether the channel is soloed.
    ///</summary>
    [ObservableProperty]
    public partial bool IsSoloed { get; set; }

    ///<summary>
    ///Display name for the channel.
    ///</summary>
    [ObservableProperty]
    public partial string Name { get; set; }

    ///<summary>
    ///Stereo pan value (-1 left to +1 right).
    ///</summary>
    [ObservableProperty]
    public partial double Pan { get; set; }

    ///<summary>
    ///Label describing the current source assigned to the channel.
    ///</summary>
    [ObservableProperty]
    public partial string SourceLabel { get; set; } = "No sample loaded";

    ///<summary>
    ///Channel volume (linear).
    ///</summary>
    [ObservableProperty]
    public partial double Volume { get; set; }
    #endregion
}
