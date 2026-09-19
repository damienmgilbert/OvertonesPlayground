namespace OvertonesPlayground.Models;

///<summary>
///The Launchpad's step sequencer: four tracks, each playing one sample, and eight patterns of steps for them.
///</summary>
public class LaunchpadSequence
{
    #region Constants
    ///<summary>
    ///The number of patterns.
    ///</summary>
    public const int PatternCount = 8;
    #endregion

    #region Public methods
    ///<summary>
    ///Makes sure there are four tracks and eight full patterns, after loading a file that may have fewer.
    ///</summary>
    public void Normalize()
    {
        while (Tracks.Count < LaunchpadPattern.TrackCount)
        {
            Tracks.Add(new LaunchpadTrack());
        }

        while (Patterns.Count < PatternCount)
        {
            Patterns.Add(new LaunchpadPattern());
        }

        foreach (LaunchpadPattern pattern in Patterns)
        {
            pattern.Normalize();
        }

        SelectedPattern = Math.Clamp(SelectedPattern, 0, PatternCount - 1);
    }
    #endregion

    #region Public properties
    ///<summary>
    ///The eight patterns.
    ///</summary>
    public List<LaunchpadPattern> Patterns { get; set; } = [.. Enumerable.Range(0, PatternCount).Select(_ => new LaunchpadPattern())];

    ///<summary>
    ///The pattern being edited and played (0 to 7).
    ///</summary>
    public int SelectedPattern { get; set; }

    ///<summary>
    ///The four tracks' samples.
    ///</summary>
    public List<LaunchpadTrack> Tracks { get; set; } = [.. Enumerable.Range(0, LaunchpadPattern.TrackCount).Select(_ => new LaunchpadTrack())];
    #endregion
}
