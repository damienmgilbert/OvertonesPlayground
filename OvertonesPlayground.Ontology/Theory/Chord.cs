namespace OvertonesPlayground.Ontology.Theory;

///<summary>
///The kind of chord: which notes sit above its root.
///</summary>
public enum ChordQuality
{
    ///<summary>Root, major third, fifth.</summary>
    Major,

    ///<summary>Root, minor third, fifth.</summary>
    Minor,

    ///<summary>Root, minor third, flattened fifth.</summary>
    Diminished,

    ///<summary>Root, major third, raised fifth.</summary>
    Augmented,

    ///<summary>Root, second, fifth.</summary>
    Sus2,

    ///<summary>Root, fourth, fifth.</summary>
    Sus4,

    ///<summary>Root and fifth only (no third, so neither major nor minor).</summary>
    Power,

    ///<summary>Major triad plus the major seventh.</summary>
    Major7,

    ///<summary>Minor triad plus the minor seventh.</summary>
    Minor7,

    ///<summary>Major triad plus the minor seventh: the chord that wants to resolve.</summary>
    Dominant7,

    ///<summary>Diminished triad plus the minor seventh (m7b5).</summary>
    HalfDiminished7,

    ///<summary>Diminished triad plus the diminished seventh.</summary>
    Diminished7,

    ///<summary>Major triad plus the sixth.</summary>
    Major6,

    ///<summary>Minor triad plus the major sixth.</summary>
    Minor6,

    ///<summary>Major seventh plus the ninth.</summary>
    Major9,

    ///<summary>Minor seventh plus the ninth.</summary>
    Minor9,

    ///<summary>Dominant seventh plus the ninth.</summary>
    Dominant9,

    ///<summary>Minor ninth plus the eleventh.</summary>
    Minor11,
}

///<summary>
///A chord: a root note and a quality. <c>Chord.Parse("BbMaj7")</c> is B flat major seventh.
///</summary>
///<param name="Root">The note the chord is built on.</param>
///<param name="Quality">Which notes sit above the root.</param>
public readonly record struct Chord(PitchClass Root, ChordQuality Quality)
{
    #region Fields
    private static readonly Dictionary<ChordQuality, int[]> _intervals = new()
    {
        [ChordQuality.Major] = [0, 4, 7],
        [ChordQuality.Minor] = [0, 3, 7],
        [ChordQuality.Diminished] = [0, 3, 6],
        [ChordQuality.Augmented] = [0, 4, 8],
        [ChordQuality.Sus2] = [0, 2, 7],
        [ChordQuality.Sus4] = [0, 5, 7],
        [ChordQuality.Power] = [0, 7],
        [ChordQuality.Major7] = [0, 4, 7, 11],
        [ChordQuality.Minor7] = [0, 3, 7, 10],
        [ChordQuality.Dominant7] = [0, 4, 7, 10],
        [ChordQuality.HalfDiminished7] = [0, 3, 6, 10],
        [ChordQuality.Diminished7] = [0, 3, 6, 9],
        [ChordQuality.Major6] = [0, 4, 7, 9],
        [ChordQuality.Minor6] = [0, 3, 7, 9],
        [ChordQuality.Major9] = [0, 4, 7, 11, 14],
        [ChordQuality.Minor9] = [0, 3, 7, 10, 14],
        [ChordQuality.Dominant9] = [0, 4, 7, 10, 14],
        [ChordQuality.Minor11] = [0, 3, 7, 10, 14, 17],
    };

    private static readonly Dictionary<ChordQuality, string> _symbols = new()
    {
        [ChordQuality.Major] = string.Empty,
        [ChordQuality.Minor] = "m",
        [ChordQuality.Diminished] = "dim",
        [ChordQuality.Augmented] = "aug",
        [ChordQuality.Sus2] = "sus2",
        [ChordQuality.Sus4] = "sus4",
        [ChordQuality.Power] = "5",
        [ChordQuality.Major7] = "maj7",
        [ChordQuality.Minor7] = "m7",
        [ChordQuality.Dominant7] = "7",
        [ChordQuality.HalfDiminished7] = "m7b5",
        [ChordQuality.Diminished7] = "dim7",
        [ChordQuality.Major6] = "6",
        [ChordQuality.Minor6] = "m6",
        [ChordQuality.Major9] = "maj9",
        [ChordQuality.Minor9] = "m9",
        [ChordQuality.Dominant9] = "9",
        [ChordQuality.Minor11] = "m11",
    };

    // Suffixes as they are written in sample names and lead sheets, matched ignoring case except where case matters (M / m).
    private static readonly (string Suffix, ChordQuality Quality)[] _suffixes =
    [
        ("maj7", ChordQuality.Major7), ("M7", ChordQuality.Major7), ("Δ7", ChordQuality.Major7), ("Δ", ChordQuality.Major7),
        ("maj9", ChordQuality.Major9), ("M9", ChordQuality.Major9),
        ("maj", ChordQuality.Major), ("M", ChordQuality.Major), (string.Empty, ChordQuality.Major),
        ("min11", ChordQuality.Minor11), ("m11", ChordQuality.Minor11),
        ("min9", ChordQuality.Minor9), ("m9", ChordQuality.Minor9),
        ("min7b5", ChordQuality.HalfDiminished7), ("m7b5", ChordQuality.HalfDiminished7), ("ø7", ChordQuality.HalfDiminished7), ("ø", ChordQuality.HalfDiminished7),
        ("min7", ChordQuality.Minor7), ("m7", ChordQuality.Minor7), ("-7", ChordQuality.Minor7),
        ("min6", ChordQuality.Minor6), ("m6", ChordQuality.Minor6),
        ("minor", ChordQuality.Minor), ("min", ChordQuality.Minor), ("m", ChordQuality.Minor), ("-", ChordQuality.Minor),
        ("dim7", ChordQuality.Diminished7), ("°7", ChordQuality.Diminished7), ("dim", ChordQuality.Diminished), ("°", ChordQuality.Diminished),
        ("aug", ChordQuality.Augmented), ("+", ChordQuality.Augmented),
        ("sus2", ChordQuality.Sus2), ("sus4", ChordQuality.Sus4), ("sus", ChordQuality.Sus4),
        ("major", ChordQuality.Major),
        ("7", ChordQuality.Dominant7), ("9", ChordQuality.Dominant9), ("6", ChordQuality.Major6), ("5", ChordQuality.Power),
    ];
    #endregion

    #region Public methods
    ///<summary>
    ///The semitones above the root of each note of a chord quality, rising (a ninth is 14).
    ///</summary>
    public static IReadOnlyList<int> IntervalsOf(ChordQuality quality) => _intervals[quality];

    ///<summary>
    ///Whether a chord quality has a minor third, so it sounds minor (or diminished).
    ///</summary>
    public static bool IsMinorQuality(ChordQuality quality) => _intervals[quality].Contains(Interval.MinorThird);

    ///<summary>
    ///Reads a chord symbol such as <c>C</c>, <c>F#m</c>, <c>Dmin7</c>, <c>BbMaj7</c>, <c>Gsus4</c> or <c>Bm7b5</c>. Returns false for
    ///anything that is not one, including a note with an octave number such as <c>C3</c>.
    ///</summary>
    public static bool TryParse(string? text, out Chord chord)
    {
        chord = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string trimmed = text.Trim();
        int rootLength = 1;
        while (rootLength < trimmed.Length && trimmed[rootLength] is '#' or '♯' or 'b' or '♭')
        {
            // "Bb" is B flat, but "Cb5" style ambiguity does not arise in practice; a lone 'b' after the letter is always a flat.
            rootLength++;
        }

        if (!PitchClass.TryParse(trimmed[..rootLength], out PitchClass root))
        {
            return false;
        }

        string suffix = trimmed[rootLength..];
        foreach ((string candidate, ChordQuality quality) in _suffixes)
        {
            bool isCaseSensitive = candidate is "M" or "M7" or "M9" or "m";
            bool matches = isCaseSensitive
                ? string.Equals(suffix, candidate, StringComparison.Ordinal)
                : string.Equals(suffix, candidate, StringComparison.OrdinalIgnoreCase);
            if (matches)
            {
                chord = new Chord(root, quality);
                return true;
            }
        }

        return false;
    }

    ///<summary>
    ///Reads a chord symbol (see <see cref="TryParse"/>).
    ///</summary>
    ///<exception cref="FormatException">The text is not a chord symbol.</exception>
    public static Chord Parse(string text) => TryParse(text, out Chord chord) ? chord : throw new FormatException($"'{text}' is not a chord symbol.");

    ///<summary>
    ///The chord named in a sample's name, if a word of it is a chord symbol with a written quality ("E-Piano LDre BbMaj7" is B flat
    ///major seventh). A bare note ("Stab Visions E") and a note with a number ("Celeste C6", "Mallet Guitar C3") are not taken as
    ///chords: in sample names those are pitches and octaves.
    ///</summary>
    public static bool TryFindInName(string? name, out Chord chord)
    {
        chord = default;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        foreach (string word in name.Split([' ', '_'], StringSplitOptions.RemoveEmptyEntries).Reverse())
        {
            string suffix = word.Length > 1 ? word[1..].TrimStart('#', 'b', '♯', '♭') : string.Empty;
            bool hasWrittenQuality = suffix.Any(char.IsLetter);
            if (hasWrittenQuality && TryParse(word, out chord))
            {
                return true;
            }
        }

        chord = default;
        return false;
    }

    ///<summary>
    ///Whether every note of the chord is in <paramref name="key"/>.
    ///</summary>
    public bool IsIn(Key key) => PitchClasses.All(key.Contains);

    ///<inheritdoc/>
    public override string ToString() => Name();

    ///<summary>
    ///The chord symbol, such as <c>Bbmaj7</c> or <c>F#m</c>.
    ///</summary>
    public string Name(bool preferFlats = false) => Root.Name(preferFlats) + _symbols[Quality];
    #endregion

    #region Public properties
    ///<summary>
    ///The chord's notes as pitch classes, root first.
    ///</summary>
    public IReadOnlyList<PitchClass> PitchClasses => [.. _intervals[Quality].Select(Root.Transpose)];

    ///<summary>Whether the chord sounds minor (has a minor third).</summary>
    public bool IsMinor => IsMinorQuality(Quality);

    ///<summary>The triad under the chord (a seventh or ninth chord reduced to root, third and fifth).</summary>
    public Chord Triad => new(Root, Quality switch
    {
        ChordQuality.Major7 or ChordQuality.Dominant7 or ChordQuality.Major6 or ChordQuality.Major9 or ChordQuality.Dominant9 => ChordQuality.Major,
        ChordQuality.Minor7 or ChordQuality.Minor6 or ChordQuality.Minor9 or ChordQuality.Minor11 => ChordQuality.Minor,
        ChordQuality.HalfDiminished7 or ChordQuality.Diminished7 => ChordQuality.Diminished,
        _ => Quality,
    });
    #endregion
}
