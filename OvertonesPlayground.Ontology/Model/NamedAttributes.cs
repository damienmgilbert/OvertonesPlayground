namespace OvertonesPlayground.Ontology.Model;

///<summary>
///Facts written in the file name: tempo, key or note, variation number and the name with those parts removed.
///</summary>
///<param name="TempoBpm">Tempo from a <c>NNN bpm</c> tag, or null.</param>
///<param name="KeyPitchClass">Pitch class (0 = C) of a trailing key or note, or null.</param>
///<param name="KeyMode">Major, minor or none.</param>
///<param name="Octave">Octave number written after a note (<c>C1</c>, <c>F#0</c>), or null. Left as written; conventions differ.</param>
///<param name="VariationNumber">Trailing number that distinguishes variations (<c>Kick Vinyl 3</c>), or null.</param>
///<param name="Stem">
///The name without tempo, key and variation number; samples with the same stem are variations of one sound.
///</param>
public sealed record NamedAttributes(double? TempoBpm, int? KeyPitchClass, KeyMode KeyMode, int? Octave, int? VariationNumber, string Stem)
{
    #region Public methods
    ///<summary>
    ///No attributes at all; only the name stem.
    ///</summary>
    public static NamedAttributes Empty(string stem) => new(null, null, KeyMode.None, null, null, stem);

    ///<summary>
    ///Key as text (<c>F# min</c>, <c>C</c>, <c>D#</c>), or null when the name has none.
    ///</summary>
    public string? KeyName()
    {
        if (KeyPitchClass is null)
        {
            return null;
        }

        string tonic = MusicalNotes.PitchClassName(KeyPitchClass.Value);
        return KeyMode switch
        {
            KeyMode.Minor => $"{tonic} min",
            KeyMode.Major => $"{tonic} maj",
            _ => tonic,
        };
    }
    #endregion
}
