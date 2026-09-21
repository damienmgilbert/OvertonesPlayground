namespace OvertonesPlayground.Ontology.Theory;

///<summary>
///A key: a tonic and the scale built on it, such as E minor or D Dorian. It knows its notes and the chords that belong to it.
///</summary>
///<param name="Tonic">The home note.</param>
///<param name="Scale">The scale built on the tonic.</param>
public sealed record Key(PitchClass Tonic, Scale Scale)
{
    #region Public methods
    ///<summary>
    ///The major key on <paramref name="tonic"/>.
    ///</summary>
    public static Key MajorOf(PitchClass tonic) => new(tonic, Scales.Major);

    ///<summary>
    ///The (natural) minor key on <paramref name="tonic"/>.
    ///</summary>
    public static Key MinorOf(PitchClass tonic) => new(tonic, Scales.Minor);

    ///<summary>
    ///Whether <paramref name="pitchClass"/> is one of the key's notes.
    ///</summary>
    public bool Contains(PitchClass pitchClass) => Scale.Contains(Tonic.SemitonesUpTo(pitchClass));

    ///<summary>
    ///Whether <paramref name="pitchClass"/> (0 to 11) is one of the key's notes.
    ///</summary>
    public bool Contains(int pitchClass) => Contains(new PitchClass(pitchClass));

    ///<summary>
    ///The note on scale degree <paramref name="degree"/> (0 is the tonic; it wraps into the next octave).
    ///</summary>
    public PitchClass NoteAt(int degree) => Tonic.Transpose(Scale.Semitones(degree));

    ///<summary>
    ///The chord built by stacking the key's own notes in thirds on degree <paramref name="degree"/>: a triad, or a seventh chord if
    ///<paramref name="sevenths"/>. Only a seven-note scale has chords like this; others return null.
    ///</summary>
    public Chord? DiatonicChord(int degree, bool sevenths = false)
    {
        if (!Scale.IsHeptatonic)
        {
            return null;
        }

        PitchClass root = NoteAt(degree);
        int third = root.SemitonesUpTo(NoteAt(degree + 2));
        int fifth = root.SemitonesUpTo(NoteAt(degree + 4));
        int seventh = root.SemitonesUpTo(NoteAt(degree + 6));
        ChordQuality? quality = (third, fifth, sevenths ? seventh : -1) switch
        {
            (4, 7, -1) => ChordQuality.Major,
            (3, 7, -1) => ChordQuality.Minor,
            (3, 6, -1) => ChordQuality.Diminished,
            (4, 8, -1) => ChordQuality.Augmented,
            (4, 7, 11) => ChordQuality.Major7,
            (4, 7, 10) => ChordQuality.Dominant7,
            (3, 7, 10) => ChordQuality.Minor7,
            (3, 6, 10) => ChordQuality.HalfDiminished7,
            (3, 6, 9) => ChordQuality.Diminished7,
            _ => null,
        };
        return quality is { } q ? new Chord(root, q) : null;
    }

    ///<summary>
    ///The key's seven chords, one per degree, as triads or seventh chords.
    ///</summary>
    public IReadOnlyList<Chord> DiatonicChords(bool sevenths = false) =>
        [.. Enumerable.Range(0, Scale.Length).Select(degree => DiatonicChord(degree, sevenths)).OfType<Chord>()];

    ///<summary>
    ///Whether the key uses flats rather than sharps when written (F major, and the keys that share its notes, use flats).
    ///</summary>
    public bool PrefersFlats
    {
        get
        {
            // The relative major decides: F, Bb, Eb, Ab, Db and Gb majors are written with flats.
            PitchClass major = Scale.IsMinor ? Tonic.Transpose(Interval.MinorThird) : Tonic;
            return major.Value is 5 or 10 or 3 or 8 or 1 or 6;
        }
    }

    ///<summary>
    ///Reads a key such as <c>E minor</c>, <c>Em</c>, <c>F#min</c>, <c>Bb major</c>, <c>C</c> or <c>D dorian</c>.
    ///</summary>
    public static bool TryParse(string? text, out Key? key)
    {
        key = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string[] parts = text.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2)
        {
            Scale? scale = Scales.Find(parts[1].Replace(' ', '-')) ?? (parts[1].ToUpperInvariant() switch
            {
                "MAJ" or "MAJOR" or "IONIAN" => Scales.Major,
                "MIN" or "MINOR" or "AEOLIAN" or "M" => Scales.Minor,
                _ => null,
            });
            if (scale is null || !PitchClass.TryParse(parts[0], out PitchClass tonic))
            {
                return false;
            }

            key = new Key(tonic, scale);
            return true;
        }

        if (!Chord.TryParse(parts[0], out Chord chord) || chord.Quality is not (ChordQuality.Major or ChordQuality.Minor))
        {
            return false;
        }

        key = chord.IsMinor ? MinorOf(chord.Root) : MajorOf(chord.Root);
        return true;
    }

    ///<summary>
    ///Reads a key (see <see cref="TryParse"/>).
    ///</summary>
    ///<exception cref="FormatException">The text is not a key.</exception>
    public static Key Parse(string text) => TryParse(text, out Key? key) ? key! : throw new FormatException($"'{text}' is not a key.");

    ///<inheritdoc/>
    public override string ToString() => Name;
    #endregion

    #region Public properties
    ///<summary>Whether the key is minor (its scale has a minor third).</summary>
    public bool IsMinor => Scale.IsMinor;

    ///<summary>
    ///The key's name, such as "E minor" or "Bb major".
    ///</summary>
    public string Name => $"{Tonic.Name(PrefersFlats)} {Scale.Name.ToLowerInvariant()}";

    ///<summary>
    ///The major or minor key with the same notes: C major for A minor, A minor for C major. Any other scale is its own relative.
    ///</summary>
    public Key Relative => Scale.Key switch
    {
        "major" => MinorOf(Tonic.Transpose(-Interval.MinorThird)),
        "minor" => MajorOf(Tonic.Transpose(Interval.MinorThird)),
        _ => this,
    };
    #endregion
}
