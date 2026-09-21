namespace OvertonesPlayground.Ontology.Theory;

///<summary>
///A scale or mode: which notes of the octave it uses, as semitones above its root. Major is 0 2 4 5 7 9 11.
///</summary>
///<param name="Key">Stable identifier, for example <c>dorian</c>.</param>
///<param name="Name">Name to show.</param>
///<param name="Steps">Semitones above the root of each degree, rising, starting with 0.</param>
public sealed record Scale(string Key, string Name, IReadOnlyList<int> Steps)
{
    #region Public methods
    ///<summary>
    ///Whether the note <paramref name="semitonesAboveRoot"/> semitones above the root (in any octave) is in the scale.
    ///</summary>
    public bool Contains(int semitonesAboveRoot) => Steps.Contains(PitchClass.Wrap(semitonesAboveRoot));

    ///<summary>
    ///How many semitones above the root degree <paramref name="degree"/> is. Degree 0 is the root; negative degrees are below it
    ///and degrees past the end continue into the next octave.
    ///</summary>
    public int Semitones(int degree)
    {
        int octave = (int)Math.Floor(degree / (double)Steps.Count);
        int within = degree - (octave * Steps.Count);
        return (octave * 12) + Steps[within];
    }

    ///<summary>
    ///The degree (0-based) of the note <paramref name="semitonesAboveRoot"/> above the root, or -1 if it is not in the scale.
    ///</summary>
    public int DegreeOf(int semitonesAboveRoot)
    {
        int wrapped = PitchClass.Wrap(semitonesAboveRoot);
        for (int i = 0; i < Steps.Count; i++)
        {
            if (Steps[i] == wrapped)
            {
                return i;
            }
        }

        return -1;
    }
    #endregion

    #region Public properties
    ///<summary>How many notes the scale has in an octave.</summary>
    public int Length => Steps.Count;

    ///<summary>Whether the scale has a minor third above its root, so it sounds minor.</summary>
    public bool IsMinor => Contains(Interval.MinorThird) && !Contains(Interval.MajorThird);

    ///<summary>Whether the scale has seven notes, so each degree can carry a chord of stacked thirds.</summary>
    public bool IsHeptatonic => Steps.Count == 7;
    #endregion
}

///<summary>
///The scales the app knows. The order is fixed: the Launchpad stores a scale by its position here, so new scales only ever go at
///the end.
///</summary>
public static class Scales
{
    #region Public properties
    ///<summary>Major (Ionian).</summary>
    public static Scale Major { get; } = new("major", "Major", [0, 2, 4, 5, 7, 9, 11]);

    ///<summary>Natural minor (Aeolian).</summary>
    public static Scale Minor { get; } = new("minor", "Minor", [0, 2, 3, 5, 7, 8, 10]);

    ///<summary>Major pentatonic: major without its fourth and seventh, so nothing in it clashes.</summary>
    public static Scale Pentatonic { get; } = new("pentatonic", "Pentatonic", [0, 2, 4, 7, 9]);

    ///<summary>Minor pentatonic plus the flattened fifth.</summary>
    public static Scale Blues { get; } = new("blues", "Blues", [0, 3, 5, 6, 7, 10]);

    ///<summary>All twelve notes.</summary>
    public static Scale Chromatic { get; } = new("chromatic", "Chromatic", [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11]);

    ///<summary>Minor with a raised sixth: jazz, funk and soul minor.</summary>
    public static Scale Dorian { get; } = new("dorian", "Dorian", [0, 2, 3, 5, 7, 9, 10]);

    ///<summary>Major with a flattened seventh: funk, rock and dominant grooves.</summary>
    public static Scale Mixolydian { get; } = new("mixolydian", "Mixolydian", [0, 2, 4, 5, 7, 9, 10]);

    ///<summary>Minor with a flattened second: dark, Spanish and metal colour.</summary>
    public static Scale Phrygian { get; } = new("phrygian", "Phrygian", [0, 1, 3, 5, 7, 8, 10]);

    ///<summary>Major with a raised fourth: bright and floating.</summary>
    public static Scale Lydian { get; } = new("lydian", "Lydian", [0, 2, 4, 6, 7, 9, 11]);

    ///<summary>Minor with a raised seventh, so its fifth chord is major and leads home strongly.</summary>
    public static Scale HarmonicMinor { get; } = new("harmonic-minor", "Harmonic minor", [0, 2, 3, 5, 7, 8, 11]);

    ///<summary>Minor pentatonic: minor without its second and sixth.</summary>
    public static Scale MinorPentatonic { get; } = new("minor-pentatonic", "Minor pentatonic", [0, 3, 5, 7, 10]);

    ///<summary>
    ///Every scale, in their fixed order.
    ///</summary>
    public static IReadOnlyList<Scale> All { get; } = [Major, Minor, Pentatonic, Blues, Chromatic, Dorian, Mixolydian, Phrygian, Lydian, HarmonicMinor, MinorPentatonic];
    #endregion

    #region Public methods
    ///<summary>
    ///The scale with key <paramref name="key"/>, ignoring case, or null.
    ///</summary>
    public static Scale? Find(string? key) => All.FirstOrDefault(scale => string.Equals(scale.Key, key, StringComparison.OrdinalIgnoreCase));

    ///<summary>
    ///The position of the scale with key <paramref name="key"/> in <see cref="All"/>, or -1.
    ///</summary>
    public static int IndexOf(string? key)
    {
        for (int i = 0; i < All.Count; i++)
        {
            if (string.Equals(All[i].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
    #endregion
}
