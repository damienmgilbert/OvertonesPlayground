namespace OvertonesPlayground.Ontology.Theory;

///<summary>
///One of the twelve notes of the octave, regardless of octave: 0 = C, 1 = C#/Db ... 11 = B. It can be written with sharps or
///flats, because the same note is F# in D major and Gb in Db major.
///</summary>
public readonly record struct PitchClass : IComparable<PitchClass>
{
    #region Fields
    private static readonly string[] _sharpNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
    private static readonly string[] _flatNames = ["C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B"];
    #endregion

    #region Constructors
    ///<summary>
    ///The pitch class <paramref name="value"/>, wrapped into 0 to 11 (so -1 is B and 12 is C).
    ///</summary>
    public PitchClass(int value) => Value = Wrap(value);
    #endregion

    #region Public methods
    ///<summary>
    ///Wraps any number of semitones into 0 to 11.
    ///</summary>
    public static int Wrap(int semitones) => ((semitones % 12) + 12) % 12;

    ///<summary>
    ///The note <paramref name="semitones"/> above this one (below, if negative).
    ///</summary>
    public PitchClass Transpose(int semitones) => new(Value + semitones);

    ///<summary>
    ///How many semitones up (0 to 11) it is from this note to <paramref name="other"/>.
    ///</summary>
    public int SemitonesUpTo(PitchClass other) => Wrap(other.Value - Value);

    ///<summary>
    ///The note's name, with a sharp or a flat when it is a black key.
    ///</summary>
    public string Name(bool preferFlats = false) => (preferFlats ? _flatNames : _sharpNames)[Value];

    ///<summary>
    ///Reads a note name: a letter A to G followed by any number of sharps (<c>#</c> or <c>♯</c>) or flats (<c>b</c> or <c>♭</c>),
    ///so <c>Bb</c>, <c>A#</c>, <c>Cb</c> and <c>E##</c> all work. Returns false if the text is not a note name.
    ///</summary>
    public static bool TryParse(string? text, out PitchClass pitchClass)
    {
        pitchClass = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string trimmed = text.Trim();
        int natural = char.ToUpperInvariant(trimmed[0]) switch
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

        int accidentals = 0;
        foreach (char c in trimmed.AsSpan(1))
        {
            switch (c)
            {
                case '#' or '♯':
                    accidentals++;
                    break;
                case 'b' or '♭':
                    accidentals--;
                    break;
                default:
                    return false;
            }
        }

        pitchClass = new PitchClass(natural + accidentals);
        return true;
    }

    ///<summary>
    ///Reads a note name (see <see cref="TryParse"/>).
    ///</summary>
    ///<exception cref="FormatException">The text is not a note name.</exception>
    public static PitchClass Parse(string text) =>
        TryParse(text, out PitchClass pitchClass) ? pitchClass : throw new FormatException($"'{text}' is not a note name.");

    ///<inheritdoc/>
    public int CompareTo(PitchClass other) => Value.CompareTo(other.Value);

    ///<inheritdoc/>
    public override string ToString() => Name();

    ///<summary>The pitch class as a number, 0 to 11.</summary>
    public static implicit operator int(PitchClass pitchClass) => pitchClass.Value;

    ///<summary>The pitch class of a number of semitones above C.</summary>
    public static explicit operator PitchClass(int value) => new(value);

    ///<summary>Compares two pitch classes by number.</summary>
    public static bool operator <(PitchClass left, PitchClass right) => left.Value < right.Value;

    ///<summary>Compares two pitch classes by number.</summary>
    public static bool operator >(PitchClass left, PitchClass right) => left.Value > right.Value;

    ///<summary>Compares two pitch classes by number.</summary>
    public static bool operator <=(PitchClass left, PitchClass right) => left.Value <= right.Value;

    ///<summary>Compares two pitch classes by number.</summary>
    public static bool operator >=(PitchClass left, PitchClass right) => left.Value >= right.Value;
    #endregion

    #region Public properties
    ///<summary>
    ///The pitch class as a number, 0 (C) to 11 (B).
    ///</summary>
    public int Value { get; }

    ///<summary>C.</summary>
    public static PitchClass C => new(0);

    ///<summary>Whether this note is a black key (C#, D#, F#, G#, A#).</summary>
    public bool IsAccidental => _sharpNames[Value].Length > 1;
    #endregion
}
