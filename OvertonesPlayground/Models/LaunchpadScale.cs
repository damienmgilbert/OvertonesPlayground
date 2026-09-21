using OvertonesPlayground.Ontology.Theory;

namespace OvertonesPlayground.Models;

///<summary>
///The scales Note and Chord modes can play in, and the pitch of each of their degrees. They are the ontology's scales
///(<see cref="Scales.All"/>), whose order never changes, because a project stores its scale by position.
///</summary>
public static class LaunchpadScale
{
    #region Public methods
    ///<summary>
    ///How many notes the scale has per octave.
    ///</summary>
    public static int Length(int scaleIndex) => Of(scaleIndex).Length;

    ///<summary>
    ///The scale's name.
    ///</summary>
    public static string Name(int scaleIndex) => Of(scaleIndex).Name;

    ///<summary>
    ///The number of scales.
    ///</summary>
    public static int Count => Scales.All.Count;

    ///<summary>
    ///The scale at <paramref name="scaleIndex"/> (wrapping round).
    ///</summary>
    public static Scale Of(int scaleIndex) => Scales.All[Wrap(scaleIndex)];

    ///<summary>
    ///The position of <paramref name="scale"/>, or 0 (major) if it is not one of the list.
    ///</summary>
    public static int IndexOf(Scale scale) => Math.Max(0, Scales.IndexOf(scale.Key));

    ///<summary>
    ///How many semitones above the root the given scale degree is. Degree 0 is the root; negative degrees are below it, and
    ///degrees past the end of the scale continue into the next octave.
    ///</summary>
    public static int Semitones(int scaleIndex, int degree) => Of(scaleIndex).Semitones(degree);

    ///<summary>
    ///The project's key, or null when it has no root note.
    ///</summary>
    public static Key? KeyOf(LaunchpadProject project) => project.RootPitchClass is { } root ? new Key(new PitchClass(root), Of(project.ScaleIndex)) : null;
    #endregion

    #region Private methods
    private static int Wrap(int scaleIndex) => ((scaleIndex % Count) + Count) % Count;
    #endregion
}
