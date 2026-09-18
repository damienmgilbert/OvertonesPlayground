using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Models;

namespace OvertonesPlayground.ViewModels;

///<summary>
///View model for one track on the Multi-Track page. Wraps a <see cref="Track"/> and exposes bindable
///volume/pan/mute/solo plus the clips placed on it.
///</summary>
public partial class TrackViewModel : ObservableObject
{
    #region Fields
    private readonly ILogger<TrackViewModel> _logger;
    #endregion

    #region Constructors
    public TrackViewModel(Track track, ILogger<TrackViewModel> logger)
    {
        Track = track;
        _logger = logger;
        Name = track.Name;
        Volume = track.Volume;
        Pan = track.Pan;
        IsMuted = track.IsMuted;
        IsSoloed = track.IsSoloed;

        foreach (TrackClip trackClip in track.Clips)
        {
            Clips.Add(new TrackClipViewModel(trackClip));
        }
    }
    #endregion

    #region Private methods
    [LoggerMessage(Level = LogLevel.Debug, Message = "Merged clips on track '{TrackName}'.")]
    private partial void Log_MergedClips(string trackName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Removed clip '{ClipName}' from track '{TrackName}'.")]
    private partial void Log_RemovedClip(string clipName, string trackName);

    [RelayCommand]
    private void MergeClips()
    {
        TimeSpan cursor = TimeSpan.Zero;
        foreach (TrackClipViewModel clipViewModel in Clips)
        {
            clipViewModel.StartOffsetSeconds = cursor.TotalSeconds;
            cursor += clipViewModel.TrackClip.Duration;
        }

        Log_MergedClips(Name);
    }

    partial void OnIsMutedChanged(bool value) => Track.IsMuted = value;

    partial void OnIsSoloedChanged(bool value) => Track.IsSoloed = value;

    partial void OnNameChanged(string value) => Track.Name = value;

    partial void OnPanChanged(double value) => Track.Pan = value;

    partial void OnVolumeChanged(double value) => Track.Volume = value;

    [RelayCommand]
    private void RemoveClip(TrackClipViewModel? clip)
    {
        if (clip is null)
        {
            return;
        }

        Track.Clips.Remove(clip.TrackClip);
        Clips.Remove(clip);
        Log_RemovedClip(clip.ClipName, Name);
    }

    [RelayCommand]
    private void ToggleMute() => IsMuted = !IsMuted;

    [RelayCommand]
    private void ToggleSolo() => IsSoloed = !IsSoloed;
    #endregion

    #region Public methods
    ///<summary>
    ///Adds a clip to this track, placed immediately after the last clip currently on it (or at the start, if empty).
    ///</summary>
    public void AddClip(AudioClip clip)
    {
        TimeSpan startOffset = Track.Clips.Count == 0 ? TimeSpan.Zero : Track.Clips[^1].StartOffset + Track.Clips[^1].Duration;
        TrackClip trackClip = new() { ClipFilePath = clip.FilePath, ClipName = clip.Name, Duration = clip.Duration, StartOffset = startOffset, };
        Track.Clips.Add(trackClip);
        Clips.Add(new TrackClipViewModel(trackClip));
    }
    #endregion

    #region Public properties
    ///<summary>
    ///Clips placed on this track, in the order they were added.
    ///</summary>
    public ObservableCollection<TrackClipViewModel> Clips { get; } = [];

    ///<summary>
    ///Set by <see cref="MultiTrackViewModel"/> to dim this strip when another track is soloed, matching the Mixer
    ///page's behavior.
    ///</summary>
    [ObservableProperty]
    public partial bool IsDimmed { get; set; }

    ///<summary>
    ///Whether this track is excluded from mixdown.
    ///</summary>
    [ObservableProperty]
    public partial bool IsMuted { get; set; }

    ///<summary>
    ///Whether this track is soloed.
    ///</summary>
    [ObservableProperty]
    public partial bool IsSoloed { get; set; }

    ///<summary>
    ///Display name for the track.
    ///</summary>
    [ObservableProperty]
    public partial string Name { get; set; }

    ///<summary>
    ///One-based position of this track on the Multi-Track page, used to build stable, predictable AutomationIds
    ///(for example "mixer-channel-2-mute").
    ///</summary>
    public int Number { get; init; }

    ///<summary>
    ///Stereo pan for this track's output, from -1 (left) to 1 (right).
    ///</summary>
    [ObservableProperty]
    public partial double Pan { get; set; }

    ///<summary>
    ///Underlying model for this track.
    ///</summary>
    public Track Track { get; }

    ///<summary>
    ///Track volume (linear), from 0 (silent) to 1 (full scale).
    ///</summary>
    [ObservableProperty]
    public partial double Volume { get; set; } = 1.0;
    #endregion
}
