using System.Text;

namespace OvertonesPlayground.Ontology.Theory;

///<summary>
///A chord written as a degree of whatever key it is played in: <c>i</c>, <c>iv7</c>, <c>V7</c>, <c>bVII</c>, <c>vii°</c>. Capitals are
///major and lower case minor; <c>°</c> is diminished, <c>ø</c> half-diminished and <c>+</c> augmented; a <c>7</c> adds the seventh
///(dominant on a capital, minor on lower case, <c>maj7</c> for a major seventh); a leading <c>b</c> or <c>#</c> moves the root a
///semitone. The degree counts through the key's own scale, so <c>VI</c> in C minor is A flat.
///</summary>
///<param name="Degree">Scale degree, 0 (I) to 6 (VII).</param>
///<param name="Accidental">Semitones the root is moved from the scale's note: -1 for <c>b</c>, 1 for <c>#</c>.</param>
///<param name="Quality">The chord's quality.</param>
public sealed record RomanNumeral(int Degree, int Accidental, ChordQuality Quality)
{
    #region Fields
    private static readonly string[] _numerals = ["I", "II", "III", "IV", "V", "VI", "VII"];
    #endregion

    #region Public methods
    ///<summary>
    ///Reads a roman numeral chord (see the type's summary). Returns false if the text is not one.
    ///</summary>
    public static bool TryParse(string? text, out RomanNumeral? numeral)
    {
        numeral = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string rest = text.Trim();
        int accidental = 0;
        while (rest.Length > 0 && rest[0] is 'b' or '#' or '♭' or '♯')
        {
            accidental += rest[0] is 'b' or '♭' ? -1 : 1;
            rest = rest[1..];
        }

        // The longest numeral first, so "VII" is not read as "V" followed by "II".
        int degree = -1;
        int length = 0;
        for (int i = _numerals.Length - 1; i >= 0; i--)
        {
            string candidate = _numerals[i];
            bool isLonger = candidate.Length > length;
            if (isLonger && rest.StartsWith(candidate, StringComparison.OrdinalIgnoreCase))
            {
                degree = i;
                length = candidate.Length;
            }
        }

        if (degree < 0)
        {
            return false;
        }

        string written = rest[..length];
        bool isUpper = written.All(char.IsUpper);
        bool isLower = written.All(char.IsLower);
        if (!isUpper && !isLower)
        {
            return false;
        }

        string suffix = rest[length..];
        ChordQuality? quality = (isUpper, suffix) switch
        {
            (true, "") => ChordQuality.Major,
            (true, "7") => ChordQuality.Dominant7,
            (true, "maj7" or "M7" or "Δ7" or "Δ") => ChordQuality.Major7,
            (true, "9") => ChordQuality.Dominant9,
            (true, "maj9") => ChordQuality.Major9,
            (true, "6") => ChordQuality.Major6,
            (true, "+") => ChordQuality.Augmented,
            (true, "sus4" or "sus") => ChordQuality.Sus4,
            (true, "sus2") => ChordQuality.Sus2,
            (true, "5") => ChordQuality.Power,
            (false, "") => ChordQuality.Minor,
            (false, "7") => ChordQuality.Minor7,
            (false, "9") => ChordQuality.Minor9,
            (false, "11") => ChordQuality.Minor11,
            (false, "6") => ChordQuality.Minor6,
            (false, "°" or "o" or "dim") => ChordQuality.Diminished,
            (false, "°7" or "o7" or "dim7") => ChordQuality.Diminished7,
            (false, "ø" or "ø7" or "m7b5") => ChordQuality.HalfDiminished7,
            _ => null,
        };
        if (quality is not { } q)
        {
            return false;
        }

        numeral = new RomanNumeral(degree, accidental, q);
        return true;
    }

    ///<summary>
    ///Reads a roman numeral chord (see <see cref="TryParse"/>).
    ///</summary>
    ///<exception cref="FormatException">The text is not a roman numeral chord.</exception>
    public static RomanNumeral Parse(string text) =>
        TryParse(text, out RomanNumeral? numeral) ? numeral! : throw new FormatException($"'{text}' is not a roman numeral chord.");

    ///<summary>
    ///The chord this numeral means in <paramref name="key"/>. A scale that is not seven notes long counts its degrees through the
    ///major (for a major-sounding scale) or minor scale on the same tonic.
    ///</summary>
    public Chord In(Key key)
    {
        Scale scale = key.Scale.IsHeptatonic ? key.Scale : key.IsMinor ? Scales.Minor : Scales.Major;
        PitchClass root = key.Tonic.Transpose(scale.Semitones(Degree) + Accidental);
        return new Chord(root, Quality);
    }

    ///<inheritdoc/>
    public override string ToString()
    {
        StringBuilder text = new();
        _ = text.Append(Accidental < 0 ? new string('b', -Accidental) : new string('#', Accidental));
        bool isMinor = Chord.IsMinorQuality(Quality);
        string numeral = _numerals[Degree];
        _ = text.Append(isMinor ? numeral.ToLowerInvariant() : numeral);
        _ = text.Append(Quality switch
        {
            ChordQuality.Dominant7 or ChordQuality.Minor7 => "7",
            ChordQuality.Major7 => "maj7",
            ChordQuality.Dominant9 or ChordQuality.Minor9 => "9",
            ChordQuality.Major9 => "maj9",
            ChordQuality.Minor11 => "11",
            ChordQuality.Major6 or ChordQuality.Minor6 => "6",
            ChordQuality.Augmented => "+",
            ChordQuality.Diminished => "°",
            ChordQuality.Diminished7 => "°7",
            ChordQuality.HalfDiminished7 => "ø7",
            ChordQuality.Sus4 => "sus4",
            ChordQuality.Sus2 => "sus2",
            ChordQuality.Power => "5",
            _ => string.Empty,
        });
        return text.ToString();
    }
    #endregion
}

///<summary>
///A chord progression in roman numerals, such as <c>i-VI-III-VII</c>, that can be played in any key.
///</summary>
///<param name="Numerals">The chords, one per bar (or per step of the harmonic rhythm).</param>
public sealed record Progression(IReadOnlyList<RomanNumeral> Numerals)
{
    #region Public methods
    ///<summary>
    ///Reads a progression written as numerals separated by dashes, spaces or commas.
    ///</summary>
    ///<exception cref="FormatException">A part is not a roman numeral chord.</exception>
    public static Progression Parse(string text) =>
        new([.. text.Split(['-', ' ', ',', '–'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(RomanNumeral.Parse)]);

    ///<summary>
    ///The progression's chords in <paramref name="key"/>.
    ///</summary>
    public IReadOnlyList<Chord> In(Key key) => [.. Numerals.Select(numeral => numeral.In(key))];

    ///<inheritdoc/>
    public override string ToString() => string.Join('-', Numerals);
    #endregion
}
