namespace OvertonesPlayground.Ontology.Classification;

///<summary>
///Votes from the words in the file name. It finds every taxonomy alias in the name, weights matches by where they sit
///(the first word of a sample-library name is nearly always the instrument, the second usually the kit), prefers the
///longest and deepest match, and drops matches that overlap the winner or merely name one of its ancestors.
///</summary>
public sealed class FilenameLexiconClassifier : ISampleClassifier
{
    #region Constants
    private const double FirstWordWeight = 1.0;
    private const double SecondWordWeight = 0.65;
    private const double LaterWordWeight = 0.45;

    private static readonly ContentType[] _contentPriority =
    [
        ContentType.Transition, ContentType.Texture, ContentType.Break, ContentType.Stab, ContentType.Phrase, ContentType.Loop,
    ];
    #endregion

    #region Private methods
    private sealed record Match<T>(T Concept, int Start, int Length, double Score)
        where T : OntologyConcept;

    private static double PositionWeight(int start) => start switch
    {
        0 => FirstWordWeight,
        1 => SecondWordWeight,
        _ => LaterWordWeight,
    };

    private static List<Match<T>> FindMatches<T>(Taxonomy<T> taxonomy, string[] words)
        where T : OntologyConcept
    {
        List<Match<T>> matches = [];
        foreach (ConceptAlias<T> alias in taxonomy.Aliases)
        {
            int length = alias.Tokens.Length;
            int lastStart = alias.StartOnly ? 0 : words.Length - length;
            for (int start = 0; start <= lastStart && start + length <= words.Length; start++)
            {
                bool isMatch = true;
                for (int i = 0; i < length; i++)
                {
                    if (words[start + i] != alias.Tokens[i])
                    {
                        isMatch = false;
                        break;
                    }
                }

                if (isMatch && !matches.Any(existing => ReferenceEquals(existing.Concept, alias.Concept) && existing.Start == start && existing.Length == length))
                {
                    double score = PositionWeight(start) * (1.0 + (0.05 * (length - 1)));
                    matches.Add(new Match<T>(alias.Concept, start, length, score));
                }
            }
        }

        return matches;
    }

    private static Match<T> Best<T>(IEnumerable<Match<T>> matches)
        where T : OntologyConcept =>
        matches
            .OrderByDescending(match => match.Score)
            .ThenByDescending(match => match.Length)
            .ThenByDescending(match => match.Concept.Depth)
            .ThenBy(match => match.Start)
            .First();

    private static bool Overlaps<T>(Match<T> a, Match<T> b)
        where T : OntologyConcept =>
        a.Start < b.Start + b.Length && b.Start < a.Start + a.Length;

    private static string Phrase<T>(Match<T> match, string[] words)
        where T : OntologyConcept =>
        string.Join(' ', words.Skip(match.Start).Take(match.Length));

    private static void ProposeInstruments(ClassificationContext context, List<ClassificationProposal> proposals)
    {
        string[] words = context.Parsed.Words;
        List<Match<InstrumentConcept>> matches = FindMatches(Taxonomies.Instruments, words);
        if (matches.Count == 0)
        {
            bool hasTempo = context.Parsed.Attributes.TempoBpm is not null;
            if (hasTempo)
            {
                proposals.Add(new ClassificationProposal(
                    ClassificationAxis.Instrument,
                    "mixed-loop",
                    0.55,
                    new Evidence(EvidenceSource.Filename, "a BPM tag but no instrument word")));
            }

            return;
        }

        Match<InstrumentConcept> winner = Best(matches);
        double winnerConfidence = Math.Min(0.97, 0.55 + (0.4 * winner.Score));
        proposals.Add(new ClassificationProposal(
            ClassificationAxis.Instrument,
            winner.Concept.Key,
            winnerConfidence,
            new Evidence(EvidenceSource.Filename, $"'{Phrase(winner, words)}' is word {winner.Start + 1}")));

        HashSet<string> seen = [winner.Concept.Key];
        foreach (Match<InstrumentConcept> other in matches
            .Where(match => !Overlaps(match, winner)
                && !match.Concept.IsA(winner.Concept)
                && !winner.Concept.IsA(match.Concept)
                && match.Score >= 0.4)
            .OrderByDescending(match => match.Score)
            .ThenByDescending(match => match.Length))
        {
            bool isNew = seen.Add(other.Concept.Key);
            if (isNew && seen.Count <= 3)
            {
                proposals.Add(new ClassificationProposal(
                    ClassificationAxis.Instrument,
                    other.Concept.Key,
                    Math.Min(0.6, 0.35 + (0.3 * other.Score)),
                    new Evidence(EvidenceSource.Filename, $"'{Phrase(other, words)}' is word {other.Start + 1}")));
            }
        }

        proposals.Add(new ClassificationProposal(
            ClassificationAxis.ContentType,
            winner.Concept.DefaultContentType.ToString(),
            0.5,
            new Evidence(EvidenceSource.Filename, $"default for {winner.Concept.DisplayName}")));
    }

    private static void ProposeKitsAndStyles(ClassificationContext context, List<ClassificationProposal> proposals)
    {
        string[] words = context.Parsed.Words;

        List<Match<KitConcept>> kits = FindMatches(Taxonomies.Kits, words);
        if (kits.Count > 0)
        {
            Match<KitConcept> kit = kits.OrderBy(match => match.Start).ThenByDescending(match => match.Length).First();
            proposals.Add(new ClassificationProposal(
                ClassificationAxis.Kit,
                kit.Concept.Key,
                kit.Start <= 1 ? 0.9 : 0.75,
                new Evidence(EvidenceSource.Filename, $"'{Phrase(kit, words)}' names a kit")));

            SoundOrigin origin = kit.Concept.Origin;
            if (origin != SoundOrigin.Unknown)
            {
                proposals.Add(new ClassificationProposal(
                    ClassificationAxis.Origin,
                    origin.ToString(),
                    0.7,
                    new Evidence(EvidenceSource.Filename, $"kit {kit.Concept.DisplayName}")));
            }
        }

        List<Match<StyleConcept>> styles = FindMatches(Taxonomies.Styles, words);
        if (styles.Count > 0)
        {
            Match<StyleConcept> style = styles.OrderBy(match => match.Start).ThenByDescending(match => match.Length).ThenByDescending(match => match.Concept.Depth).First();
            proposals.Add(new ClassificationProposal(
                ClassificationAxis.Style,
                style.Concept.Key,
                0.75,
                new Evidence(EvidenceSource.Filename, $"'{Phrase(style, words)}' names a style")));
        }
    }

    private static void ProposeContentAndOrigin(ClassificationContext context, List<ClassificationProposal> proposals)
    {
        string[] words = context.Parsed.Words;
        Lexicon lexicon = Taxonomies.Lexicon;

        foreach (ContentType contentType in _contentPriority)
        {
            if (!lexicon.ContentTypeKeywords.TryGetValue(contentType, out HashSet<string>? keywords))
            {
                continue;
            }

            int index = Array.FindIndex(words, keywords.Contains);
            if (index >= 0)
            {
                proposals.Add(new ClassificationProposal(
                    ClassificationAxis.ContentType,
                    contentType.ToString(),
                    index == 0 ? 0.85 : 0.7,
                    new Evidence(EvidenceSource.Filename, $"'{words[index]}' suggests {contentType}")));
            }
        }

        if (context.Parsed.Attributes.TempoBpm is not null)
        {
            proposals.Add(new ClassificationProposal(
                ClassificationAxis.ContentType,
                ContentType.Loop.ToString(),
                0.65,
                new Evidence(EvidenceSource.Filename, "has a BPM tag")));
        }

        foreach ((SoundOrigin origin, HashSet<string> keywords) in lexicon.OriginKeywords)
        {
            int index = Array.FindIndex(words, keywords.Contains);
            if (index >= 0)
            {
                proposals.Add(new ClassificationProposal(
                    ClassificationAxis.Origin,
                    origin.ToString(),
                    0.6,
                    new Evidence(EvidenceSource.Filename, $"'{words[index]}' suggests {origin}")));
            }
        }
    }
    #endregion

    #region Public methods
    ///<inheritdoc/>
    public IReadOnlyList<ClassificationProposal> Classify(ClassificationContext context)
    {
        List<ClassificationProposal> proposals = [];
        ProposeInstruments(context, proposals);
        ProposeKitsAndStyles(context, proposals);
        ProposeContentAndOrigin(context, proposals);
        return proposals;
    }
    #endregion
}
