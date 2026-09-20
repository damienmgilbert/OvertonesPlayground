using System.Text.RegularExpressions;

namespace OvertonesPlayground.Ontology.Classification;

///<summary>
///Understands the naming conventions of the bundled sample library: <c>Bass Sub C#0</c>, <c>Banjo Arpeggio E Minor 100
///bpm</c>, <c>Kick 909 DMX 1</c>, <c>Kick 808 Tone11</c>, <c>808 Heavy E</c>. A lower-case letter after a knob name (<c>Kick 909 Tune1 d</c>) is a
///control setting, not a note, and is left alone.
///</summary>
public static partial class FilenameParser
{
    private static readonly Lazy<HashSet<string>> _kitWords = new(() =>
    [.. Taxonomies.Kits.Aliases.Where(alias => alias.Tokens.Length == 1).Select(alias => alias.Tokens[0])]);

    [GeneratedRegex(@"(?<!\d)(\d{2,3})\s?bpm\b", RegexOptions.IgnoreCase)]
    private static partial Regex TempoPattern();

    [GeneratedRegex(@"^([A-G][#b]?)(-?\d)$")]
    private static partial Regex NoteWithOctavePattern();

    [GeneratedRegex(@"^([A-G][#b]?)(min|maj|minor|major)$", RegexOptions.IgnoreCase)]
    private static partial Regex NoteWithModePattern();

    [GeneratedRegex(@"^(min|maj|minor|major)$", RegexOptions.IgnoreCase)]
    private static partial Regex ModePattern();

    [GeneratedRegex(@"^[A-G][#b]?$")]
    private static partial Regex BareNotePattern();

    [GeneratedRegex(@"^\d{1,2}$")]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"^([A-Za-z]+)(\d{1,2})$")]
    private static partial Regex WordWithNumberPattern();

    private static KeyMode ParseMode(string text) =>
        text.StartsWith("min", StringComparison.OrdinalIgnoreCase) ? KeyMode.Minor : KeyMode.Major;

    ///<summary>Parses a file name (without extension).</summary>
    public static ParsedName Parse(string name)
    {
        double? tempo = null;
        string working = name;
        Match tempoMatch = TempoPattern().Match(working);
        if (tempoMatch.Success)
        {
            tempo = double.Parse(tempoMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            working = working.Remove(tempoMatch.Index, tempoMatch.Length);
        }

        List<string> tokens = [.. working.Split([' ', '_'], StringSplitOptions.RemoveEmptyEntries)];
        int? pitchClass = null;
        int? octave = null;
        KeyMode mode = KeyMode.None;
        int? variation = null;
        bool variationStripped = false;
        int originalCount = tokens.Count;

        for (int pass = 0; pass < 3 && tokens.Count > 1; pass++)
        {
            string last = tokens[^1];
            string? previous = tokens.Count >= 2 ? tokens[^2] : null;
            bool canTakeKey = pitchClass is null;

            Match withOctave = NoteWithOctavePattern().Match(last);
            Match withMode = NoteWithModePattern().Match(last);

            if (canTakeKey && withOctave.Success)
            {
                _ = MusicalNotes.TryParsePitchClass(withOctave.Groups[1].Value, out int pc);
                pitchClass = pc;
                octave = int.Parse(withOctave.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                tokens.RemoveAt(tokens.Count - 1);
            }
            else if (canTakeKey && withMode.Success)
            {
                _ = MusicalNotes.TryParsePitchClass(withMode.Groups[1].Value, out int pc);
                pitchClass = pc;
                mode = ParseMode(withMode.Groups[2].Value);
                tokens.RemoveAt(tokens.Count - 1);
            }
            else if (canTakeKey && ModePattern().IsMatch(last) && previous is not null && BareNotePattern().IsMatch(previous) && tokens.Count >= 3)
            {
                _ = MusicalNotes.TryParsePitchClass(previous, out int pc);
                pitchClass = pc;
                mode = ParseMode(last);
                tokens.RemoveRange(tokens.Count - 2, 2);
            }
            else if (canTakeKey && BareNotePattern().IsMatch(last) && !variationStripped && (last.Length == 2 || tempo is not null || originalCount >= 3))
            {
                _ = MusicalNotes.TryParsePitchClass(last, out int pc);
                pitchClass = pc;
                tokens.RemoveAt(tokens.Count - 1);
            }
            else if (variation is null && NumberPattern().IsMatch(last))
            {
                variation = int.Parse(last, System.Globalization.CultureInfo.InvariantCulture);
                variationStripped = true;
                tokens.RemoveAt(tokens.Count - 1);
            }
            else if (variation is null && WordWithNumberPattern().Match(last) is { Success: true } wordWithNumber
                && !_kitWords.Value.Contains(last.ToLowerInvariant()))
            {
                variation = int.Parse(wordWithNumber.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                variationStripped = true;
                tokens[^1] = wordWithNumber.Groups[1].Value;
            }
            else
            {
                break;
            }
        }

        string[] words = TextNormalizer.Tokenize(string.Join(' ', tokens));
        NamedAttributes attributes = new(tempo, pitchClass, mode, octave, variation, string.Join(' ', words));
        return new ParsedName(words, attributes);
    }
}
