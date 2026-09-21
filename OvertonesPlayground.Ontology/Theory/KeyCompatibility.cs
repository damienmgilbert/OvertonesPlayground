namespace OvertonesPlayground.Ontology.Theory;

///<summary>
///How a sample sits with a key.
///</summary>
public enum KeyFit
{
    ///<summary>The sample has no pitch that could clash (a drum, a noise, or a sound whose pitch is unknown).</summary>
    Neutral,

    ///<summary>Everything the sample is known to play is in the key.</summary>
    Fits,

    ///<summary>The sample plays a note or chord outside the key.</summary>
    Clashes,
}

///<summary>
///Judges whether a sample can sound in a key, from the chord or key written in its name or, for a melodic instrument, from its
///measured pitch. The rules are strict for loops and phrases, which play a whole passage in their key, and looser for one-shots,
///which play a single note or chord:
///<list type="bullet">
///<item>a one-shot chord ("E-Piano Dmin7") fits when its triad is made of the key's notes, so the key's own chords all fit;</item>
///<item>a one-shot named after a note, or a melodic one-shot with a confidently measured pitch, fits when that note is in the key;</item>
///<item>a loop in a minor key must be in the key itself (a minor key) or its relative minor (a major key), and a loop in a major key
///likewise; a loop named after a note only needs that note in the key;</item>
///<item>a drum one-shot is neutral even if its name looks like a note ("Snare DMX A1" is a model number, not the note A);</item>
///<item>anything else with no known pitch is neutral.</item>
///</list>
///</summary>
public static class KeyCompatibility
{
    #region Constants
    ///<summary>The confidence a measured pitch needs before it is trusted to judge a melodic sound.</summary>
    public const double MinPitchConfidence = 0.9;

    private static readonly HashSet<string> _melodicCategories = new(StringComparer.Ordinal) { "keys", "strings-plucked", "brass-winds", "synth", "bass", "stab" };
    #endregion

    #region Public methods
    ///<summary>
    ///Whether a sample plays notes: a keyboard, string, wind, synth, bass or stab, or a tuned percussion instrument.
    ///</summary>
    public static bool IsMelodic(Sample sample)
    {
        string instrument = sample.Classification.Instrument.Value;
        if (!Taxonomies.Instruments.TryGet(instrument, out InstrumentConcept? concept) || concept.Depth < 1)
        {
            return false;
        }

        return _melodicCategories.Contains(concept.Category.Key) || concept.IsA("tuned-percussion");
    }

    ///<summary>
    ///Whether a sample is a drum or unpitched percussion (tuned percussion such as a mallet or bell is not).
    ///</summary>
    public static bool IsDrum(Sample sample) =>
        Taxonomies.Instruments.TryGet(sample.Classification.Instrument.Value, out InstrumentConcept? concept) && concept.IsA("percussion") && !concept.IsA("tuned-percussion");

    ///<summary>
    ///Whether a sample plays a whole passage (a loop, break or phrase) rather than a single note or chord.
    ///</summary>
    public static bool IsPassage(Sample sample) => sample.Classification.ContentType.Value is ContentType.Loop or ContentType.Break or ContentType.Phrase;

    ///<summary>
    ///How <paramref name="sample"/> sits with <paramref name="key"/> (see the type's summary).
    ///</summary>
    public static KeyFit Judge(Sample sample, Key key)
    {
        NamedAttributes attributes = sample.Classification.Attributes;
        bool isPassage = IsPassage(sample);
        if (!isPassage && IsDrum(sample))
        {
            return KeyFit.Neutral;
        }

        if (!isPassage && Chord.TryFindInName(sample.Name, out Chord chord))
        {
            return chord.Triad.IsIn(key) ? KeyFit.Fits : KeyFit.Clashes;
        }

        if (attributes.KeyPitchClass is { } named)
        {
            PitchClass pitchClass = new(named);
            if (!key.Contains(pitchClass))
            {
                return KeyFit.Clashes;
            }

            bool fits = (attributes.KeyMode, isPassage) switch
            {
                (KeyMode.Minor, true) => pitchClass == (key.IsMinor ? key.Tonic : key.Relative.Tonic),
                (KeyMode.Major, true) => pitchClass == (key.IsMinor ? key.Relative.Tonic : key.Tonic),
                (KeyMode.Minor, false) => new Chord(pitchClass, ChordQuality.Minor).IsIn(key),
                (KeyMode.Major, false) => new Chord(pitchClass, ChordQuality.Major).IsIn(key),
                _ => true,
            };
            return fits ? KeyFit.Fits : KeyFit.Clashes;
        }

        bool hasTrustedPitch = !isPassage && IsMelodic(sample) && sample.Tonality is { PitchConfidence: >= MinPitchConfidence, PitchClass: not null };
        if (hasTrustedPitch)
        {
            return key.Contains(sample.Tonality!.PitchClass!.Value) ? KeyFit.Fits : KeyFit.Clashes;
        }

        return KeyFit.Neutral;
    }

    ///<summary>
    ///Whether <paramref name="sample"/> can sound in <paramref name="key"/> without clashing.
    ///</summary>
    public static bool CanPlayIn(Sample sample, Key key) => Judge(sample, key) != KeyFit.Clashes;

    ///<summary>
    ///The note or chord root a sample plays, if it is known: from a chord or key in its name, or a confidently measured pitch of a
    ///melodic sound.
    ///</summary>
    public static PitchClass? PitchOf(Sample sample)
    {
        if (!IsPassage(sample) && IsDrum(sample))
        {
            return null;
        }

        if (!IsPassage(sample) && Chord.TryFindInName(sample.Name, out Chord chord))
        {
            return chord.Root;
        }

        if (sample.Classification.Attributes.KeyPitchClass is { } named)
        {
            return new PitchClass(named);
        }

        return IsMelodic(sample) && sample.Tonality is { PitchConfidence: >= MinPitchConfidence, PitchClass: { } measured } ? new PitchClass(measured) : null;
    }
    #endregion
}
