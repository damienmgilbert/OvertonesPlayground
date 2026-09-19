namespace OvertonesPlayground.Models;

///<summary>
///One sequencer pattern: up to 32 steps for each of the four tracks, plus how the pattern is played.
///</summary>
public class LaunchpadPattern
{
    #region Constants
    ///<summary>
    ///The most steps a pattern can have.
    ///</summary>
    public const int StepCount = 32;

    ///<summary>
    ///The number of tracks.
    ///</summary>
    public const int TrackCount = 4;
    #endregion

    #region Public methods
    ///<summary>
    ///Returns an independent copy of this pattern.
    ///</summary>
    public LaunchpadPattern Clone() => new()
    {
        Length = Length,
        Direction = Direction,
        Speed = Speed,
        Tracks = [.. Tracks.Select(track => track.Select(step => step.Clone()).ToList())],
    };

    ///<summary>
    ///Makes sure every track has exactly <see cref="StepCount"/> steps, after loading a file that may have fewer.
    ///</summary>
    public void Normalize()
    {
        while (Tracks.Count < TrackCount)
        {
            Tracks.Add(NewTrack());
        }

        foreach (List<LaunchpadStep> track in Tracks)
        {
            while (track.Count < StepCount)
            {
                track.Add(new LaunchpadStep());
            }
        }

        Length = Math.Clamp(Length, 1, StepCount);
    }

    ///<summary>
    ///Creates a track of empty steps.
    ///</summary>
    public static List<LaunchpadStep> NewTrack() => [.. Enumerable.Range(0, StepCount).Select(_ => new LaunchpadStep())];
    #endregion

    #region Public properties
    ///<summary>
    ///The order the steps are played in.
    ///</summary>
    public PatternDirection Direction { get; set; }

    ///<summary>
    ///How many steps the pattern plays before going round again (1 to <see cref="StepCount"/>).
    ///</summary>
    public int Length { get; set; } = 16;

    ///<summary>
    ///How fast the pattern plays compared with the tempo: 0.5, 1 or 2.
    ///</summary>
    public double Speed { get; set; } = 1;

    ///<summary>
    ///The steps of each track.
    ///</summary>
    public List<List<LaunchpadStep>> Tracks { get; set; } = [.. Enumerable.Range(0, TrackCount).Select(_ => NewTrack())];
    #endregion
}
