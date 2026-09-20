namespace OvertonesPlayground.Ontology.Facets;

///<summary>
///Conversions between frequency, MIDI note numbers and note names (A4 = 440 Hz = MIDI 69).
///</summary>
public static class MusicalNotes
{
    #region Constants
    private static readonly string[] _pitchClassNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
    #endregion

    #region Public methods
    ///<summary>Frequency of <paramref name="midiNote"/> in hertz.</summary>
    public static double FrequencyOf(double midiNote) => 440.0 * Math.Pow(2.0, (midiNote - 69.0) / 12.0);

    ///<summary>Fractional MIDI note number of <paramref name="hz"/>.</summary>
    public static double MidiOf(double hz) => 69.0 + (12.0 * Math.Log2(hz / 440.0));

    ///<summary>Name of a pitch class, 0 = C ... 11 = B, using sharps.</summary>
    public static string PitchClassName(int pitchClass) => _pitchClassNames[((pitchClass % 12) + 12) % 12];

    ///<summary>Name with octave, for example <c>C#4</c> (MIDI 61). Octave numbering makes MIDI 60 = C4.</summary>
    public static string NoteName(int midiNote) => PitchClassName(midiNote) + (Math.DivRem(midiNote, 12, out _) - 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

    ///<summary>Pitch class (0 - 11) of <paramref name="midiNote"/>.</summary>
    public static int PitchClassOf(int midiNote) => ((midiNote % 12) + 12) % 12;

    ///<summary>
    ///Parses a pitch class such as <c>C</c>, <c>F#</c> or <c>Bb</c>. Returns false when the text is not a note name.
    ///</summary>
    public static bool TryParsePitchClass(string text, out int pitchClass)
    {
        pitchClass = 0;
        bool isTooShort = text.Length is < 1 or > 2;
        if (isTooShort)
        {
            return false;
        }

        int natural = char.ToUpperInvariant(text[0]) switch
        {
            'C' => 0,
            'D' => 2,
            'E' => 4,
            'F' => 5,
            'G' => 7,
            'A' => 9,
            'B' => 11,
            _ => -1,
        };
        if (natural < 0)
        {
            return false;
        }

        int accidental = 0;
        if (text.Length == 2)
        {
            switch (text[1])
            {
                case '#':
                    accidental = 1;
                    break;
                case 'b':
                    accidental = -1;
                    break;
                default:
                    return false;
            }
        }

        pitchClass = (((natural + accidental) % 12) + 12) % 12;
        return true;
    }
    #endregion
}
