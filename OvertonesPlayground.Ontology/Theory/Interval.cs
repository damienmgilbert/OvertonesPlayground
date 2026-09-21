namespace OvertonesPlayground.Ontology.Theory;

///<summary>
///The distance between two notes in semitones, with the names musicians use for it.
///</summary>
public static class Interval
{
    #region Constants
    ///<summary>The same note.</summary>
    public const int Unison = 0;

    ///<summary>One semitone.</summary>
    public const int MinorSecond = 1;

    ///<summary>Two semitones: a whole tone.</summary>
    public const int MajorSecond = 2;

    ///<summary>Three semitones: the third of a minor chord.</summary>
    public const int MinorThird = 3;

    ///<summary>Four semitones: the third of a major chord.</summary>
    public const int MajorThird = 4;

    ///<summary>Five semitones.</summary>
    public const int PerfectFourth = 5;

    ///<summary>Six semitones: the tritone, half an octave.</summary>
    public const int Tritone = 6;

    ///<summary>Seven semitones: the most consonant interval after the octave.</summary>
    public const int PerfectFifth = 7;

    ///<summary>Eight semitones.</summary>
    public const int MinorSixth = 8;

    ///<summary>Nine semitones.</summary>
    public const int MajorSixth = 9;

    ///<summary>Ten semitones: the seventh of a dominant or minor seventh chord.</summary>
    public const int MinorSeventh = 10;

    ///<summary>Eleven semitones: the seventh of a major seventh chord.</summary>
    public const int MajorSeventh = 11;

    ///<summary>Twelve semitones.</summary>
    public const int Octave = 12;
    #endregion

    #region Fields
    private static readonly string[] _names =
    [
        "unison", "minor second", "major second", "minor third", "major third", "perfect fourth", "tritone",
        "perfect fifth", "minor sixth", "major sixth", "minor seventh", "major seventh",
    ];
    #endregion

    #region Public methods
    ///<summary>
    ///The name of an interval of <paramref name="semitones"/>, taken within one octave ("perfect fifth" for 7 or 19).
    ///</summary>
    public static string Name(int semitones) => _names[PitchClass.Wrap(semitones)];

    ///<summary>
    ///Whether two notes played together sound stable: a unison, third, fourth, fifth, sixth or octave.
    ///</summary>
    public static bool IsConsonant(int semitones) => PitchClass.Wrap(semitones) is Unison or MinorThird or MajorThird or PerfectFourth or PerfectFifth or MinorSixth or MajorSixth;

    ///<summary>
    ///Whether two notes a semitone or a tritone apart clash hard when they sound together (the "avoid" intervals).
    ///</summary>
    public static bool IsHarsh(int semitones) => PitchClass.Wrap(semitones) is MinorSecond or Tritone or MajorSeventh;
    #endregion
}
