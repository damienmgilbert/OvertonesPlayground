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
    private readonly ILogger<MixerChannelViewModel> _logger;
    private readonly IAudioPlaybackService _playbackService;
    #endregion

    #region Constructors
    public MixerChannelViewModel(MixerChannelStrip channel, IAudioPlaybackService playbackService, ILogger<MixerChannelViewModel> logger)
    {
        Channel = channel;
        _playbackService = playbackService;
        _logger = logger;
        Name = channel.Name;
        Volume = channel.Volume;
        Pan = channel.Pan;
        IsMuted = channel.IsMuted;
        IsSoloed = channel.IsSoloed;
        Log_ChannelCreated(Name);
    }
    #endregion

    #region Private methods
    [LoggerMessage(Level = LogLevel.Debug, Message = "Channel '{ChannelName}' created.")]
    private partial void Log_ChannelCreated(string channelName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Channel '{ChannelName}' mute set to {IsMuted}.")]
    private partial void Log_ChannelMuteChanged(string channelName, bool isMuted);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Channel '{ChannelName}' playback started.")]
    private partial void Log_ChannelPlaybackStarted(string channelName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Channel '{ChannelName}' playback stopped.")]
    private partial void Log_ChannelPlaybackStopped(string channelName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Channel '{ChannelName}' solo set to {IsSoloed}.")]
    private partial void Log_ChannelSoloChanged(string channelName, bool isSoloed);

    ///<summary>
    ///Being dimmed means another channel is soloed, so this one must go quiet too, not just look faded.
    ///</summary>
    partial void OnIsDimmedChanged(bool value)
    {
        Channel.IsSilencedBySolo = value;
        _playbackService.UpdateChannel(Channel);
    }

    partial void OnIsMutedChanged(bool value)
    {
        Channel.IsMuted = value;
        _playbackService.UpdateChannel(Channel);
    }

    partial void OnIsSoloedChanged(bool value) => Channel.IsSoloed = value;

    partial void OnNameChanged(string value) => Channel.Name = value;

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
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
        Log_ChannelMuteChanged(Name, IsMuted);
    }

    [RelayCommand]
    private void TogglePlayback()
    {
        if (!HasSource)
        {
            return;
        }

        if (IsPlaying)
        {
            Stop();
        }
        else
        {
            Log_ChannelPlaybackStarted(Name);
            _playbackService.PlayChannel(Channel);
            IsPlaying = true;
        }
    }

    [RelayCommand]
    private void ToggleSolo()
    {
        IsSoloed = !IsSoloed;
        Log_ChannelSoloChanged(Name, IsSoloed);
    }
    #endregion

    #region Public methods
    public void AssignSource(string filePath, string label)
    {
        // Loading a new sample shouldn't start it blaring - the user starts it with the toggle.
        if (IsPlaying)
        {
            Stop();
        }

        Channel.SourceClipPath = filePath;
        SourceLabel = label;
        OnPropertyChanged(nameof(HasSource));
    }

    public void Stop()
    {
        Log_ChannelPlaybackStopped(Name);
        _playbackService.StopChannel(Channel.Id);
        IsPlaying = false;
    }
    #endregion

    #region Public properties
    ///<summary>
    ///Underlying model for this channel.
    ///</summary>
    public MixerChannelStrip Channel { get; }

    ///<summary>
    ///Hex color identifying this channel strip, shown as a colored tab at the top of the strip.
    ///</summary>
    public string ColorHex => Channel.ColorHex;

    public bool HasSource => !string.IsNullOrEmpty(Channel.SourceClipPath);

    ///<summary>
    ///Set by <see cref="MixerViewModel"/> when another channel is soloed. The strip is dimmed and its voice is silenced,
    ///matching how DAWs like Ableton and Audacity treat a channel silenced by another track's solo.
    ///</summary>
    [ObservableProperty]
    public partial bool IsDimmed { get; set; }

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
    ///One-based position of this channel in the mixer, used to build stable, predictable AutomationIds
    ///(for example "mixer-channel-2-mute").
    ///</summary>
    public int Number { get; init; }

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
