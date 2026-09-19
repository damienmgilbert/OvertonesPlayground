namespace OvertonesPlayground.Models;

///<summary>
///The scales Note and Chord modes can play in, and the pitch of each of their degrees.
///</summary>
public static class LaunchpadScale
{
    #region Fields
    private static readonly string[] ScaleNames = ["Major", "Minor", "Pentatonic", "Blues", "Chromatic"];

    private static readonly int[][] Intervals =
    [
        [0, 2, 4, 5, 7, 9, 11],
        [0, 2, 3, 5, 7, 8, 10],
        [0, 2, 4, 7, 9],
        [0, 3, 5, 6, 7, 10],
        [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11],
    ];
    #endregion

    #region Public methods
    ///<summary>
    ///How many notes the scale has per octave.
    ///</summary>
    public static int Length(int scaleIndex) => Intervals[Wrap(scaleIndex)].Length;

    ///<summary>
    ///The scale's name.
    ///</summary>
    public static string Name(int scaleIndex) => ScaleNames[Wrap(scaleIndex)];

    ///<summary>
    ///The number of scales.
    ///</summary>
    public static int Count => ScaleNames.Length;

    ///<summary>
    ///How many semitones above the root the given scale degree is. Degree 0 is the root; negative degrees are below it, and
    ///degrees past the end of the scale continue into the next octave.
    ///</summary>
    public static int Semitones(int scaleIndex, int degree)
    {
        int[] notes = Intervals[Wrap(scaleIndex)];
        int octave = (int)Math.Floor(degree / (double)notes.Length);
        int within = degree - (octave * notes.Length);
        return (octave * 12) + notes[within];
    }
    #endregion

    #region Private methods
    private static int Wrap(int scaleIndex) => ((scaleIndex % ScaleNames.Length) + ScaleNames.Length) % ScaleNames.Length;
    #endregion
}
