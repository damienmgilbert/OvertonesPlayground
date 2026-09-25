using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using OvertonesPlayground.Models;

namespace OvertonesPlayground.ViewModels;

///<summary>
///View model for one clip placed on a <see cref="TrackViewModel"/>. Wraps a <see cref="TrackClip"/> and exposes
///bindable start-offset/gain for the Multi-Track page.
///</summary>
public partial class TrackClipViewModel : ObservableObject
{
    #region Constructors
    public TrackClipViewModel(TrackClip trackClip)
    {
        TrackClip = trackClip;
        GainDb = trackClip.GainDb;
        StartOffsetSeconds = trackClip.StartOffset.TotalSeconds;
    }
    #endregion

    #region Private methods
    partial void OnGainDbChanged(double value) => TrackClip.GainDb = value;

    partial void OnStartOffsetSecondsChanged(double value) => TrackClip.StartOffset = TimeSpan.FromSeconds(Math.Max(0, value));
    #endregion

    #region Public properties
    ///<summary>
    ///Display name of the source clip.
    ///</summary>
    public string ClipName => TrackClip.ClipName;

    ///<summary>
    ///The source clip's duration, formatted as "mm:ss".
    ///</summary>
    public string DurationText => TrackClip.Duration.ToString(@"mm\:ss", CultureInfo.InvariantCulture);

    ///<summary>
    ///Gain applied to this clip within the track, in decibels.
    ///</summary>
    [ObservableProperty]
    public partial double GainDb { get; set; }

    ///<summary>
    ///Where this clip starts playing, in seconds relative to the start of the track.
    ///</summary>
    [ObservableProperty]
    public partial double StartOffsetSeconds { get; set; }

    ///<summary>
    ///The wrapped model.
    ///</summary>
    public TrackClip TrackClip { get; }
    #endregion
}
