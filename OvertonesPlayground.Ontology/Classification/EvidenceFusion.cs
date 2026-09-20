namespace OvertonesPlayground.Ontology.Classification;

///<summary>
///Reconciles the votes of all classifiers into one <see cref="SampleClassification"/>. Votes for the same value from
///different sources combine like independent witnesses (1 - the product of doubts); the strongest value wins. Where
///the name and the audio disagree, or nothing is confident, the sample is flagged for human review instead of the
///conflict being hidden. Manual overrides are applied last.
///</summary>
public sealed class EvidenceFusion
{
    #region Constants
    private const double SignalWeight = 0.6;
    private const double DisagreementConfidence = 0.6;
    private const double UncertainBelow = 0.5;
    private const string Unclassified = "unclassified";
    private const double SignalOnlyAcceptance = 0.6;
    private const string MixedLoop = "mixed-loop";
    private const string DrumLoop = "drum-loop";
    #endregion

    #region Private methods
    ///<summary>Noisy-or across sources; within one source only the strongest vote counts.</summary>
    private static (double Confidence, List<Evidence> Evidence) Combine(IEnumerable<ClassificationProposal> votes, double signalWeight = 1.0)
    {
        double doubt = 1.0;
        List<Evidence> evidence = [];
        foreach (IGrouping<EvidenceSource, ClassificationProposal> bySource in votes.GroupBy(vote => vote.Evidence.Source))
        {
            ClassificationProposal strongest = bySource.OrderByDescending(vote => vote.Confidence).First();
            double weight = strongest.Evidence.Source == EvidenceSource.Signal ? signalWeight : 1.0;
            doubt *= 1.0 - (strongest.Confidence * weight);
            evidence.Add(strongest.Evidence);
        }

        return (1.0 - doubt, evidence);
    }

    private static Label<string>? Pick(List<ClassificationProposal> proposals, ClassificationAxis axis)
    {
        IEnumerable<IGrouping<string, ClassificationProposal>> groups = proposals.Where(p => p.Axis == axis).GroupBy(p => p.Value);
        Label<string>? best = null;
        foreach (IGrouping<string, ClassificationProposal> group in groups)
        {
            (double confidence, List<Evidence> evidence) = Combine(group);
            if (best is null || confidence > best.Confidence)
            {
                best = new Label<string>(group.Key, Decibels.Round(confidence, 3), evidence);
            }
        }

        return best;
    }

    private static Label<TEnum> PickEnum<TEnum>(List<ClassificationProposal> proposals, ClassificationAxis axis, TEnum fallback)
        where TEnum : struct, Enum
    {
        Label<string>? picked = Pick(proposals, axis);
        return picked is null || !Enum.TryParse(picked.Value, out TEnum value)
            ? new Label<TEnum>(fallback, 0.0, [])
            : new Label<TEnum>(value, picked.Confidence, picked.Evidence);
    }

    private static bool AreRelated(InstrumentConcept a, InstrumentConcept b) => a.IsA(b) || b.IsA(a);

    private static (Label<string> Primary, List<Label<string>> Alternates, string? Disagreement, string? Suggestion) PickInstrument(List<ClassificationProposal> proposals)
    {
        Taxonomy<InstrumentConcept> taxonomy = Taxonomies.Instruments;
        List<ClassificationProposal> byName = [.. proposals.Where(p => p.Axis == ClassificationAxis.Instrument && p.Evidence.Source != EvidenceSource.Signal)];
        List<ClassificationProposal> bySignal = [.. proposals.Where(p => p.Axis == ClassificationAxis.Instrument && p.Evidence.Source == EvidenceSource.Signal)];

        List<Label<string>> candidates = [];
        foreach (IGrouping<string, ClassificationProposal> group in byName.GroupBy(p => p.Value))
        {
            InstrumentConcept concept = taxonomy.Get(group.Key);
            List<ClassificationProposal> support = [.. group];
            foreach (ClassificationProposal signal in bySignal.Where(s => taxonomy.TryGet(s.Value, out InstrumentConcept? family) && concept.IsA(family)))
            {
                support.Add(signal);
            }

            (double confidence, List<Evidence> evidence) = Combine(support, SignalWeight);
            candidates.Add(new Label<string>(group.Key, Decibels.Round(confidence, 3), evidence));
        }

        if (candidates.Count == 0)
        {
            // Nothing in the name. Believe the audio alone only when it is quite sure; otherwise keep the guess as an
            // alternate (so it is visible) but leave the sample unclassified rather than mislabel it, for example calling a
            // vinyl-crackle texture a hi-hat.
            List<ClassificationProposal> guesses = [.. bySignal.OrderByDescending(p => p.Confidence)];
            bool isConfident = guesses.Count > 0 && guesses[0].Confidence >= SignalOnlyAcceptance;
            if (isConfident)
            {
                candidates.Add(new Label<string>(guesses[0].Value, Decibels.Round(guesses[0].Confidence * SignalWeight, 3), [guesses[0].Evidence]));
            }
            else
            {
                List<Label<string>> asAlternates = [.. guesses.Take(2).Select(g => new Label<string>(g.Value, Decibels.Round(g.Confidence, 3), [g.Evidence]))];
                string? suggestion = guesses.Count > 0 && taxonomy.TryGet(guesses[0].Value, out InstrumentConcept? guess)
                    ? $"the name has no instrument word and the audio only suggests {guess.DisplayName} ({guesses[0].Confidence:0.00})"
                    : "the name has no instrument word and the audio gives no clear answer";
                return (new Label<string>(Unclassified, 0.0, []), asAlternates, null, suggestion);
            }
        }

        List<Label<string>> ordered = [.. candidates.OrderByDescending(label => label.Confidence).ThenByDescending(label => taxonomy.Get(label.Value).Depth)];

        // "A loop with a BPM tag" is only a guess at what the loop contains; when the audio clearly says drums, believe it.
        ClassificationProposal? drumLoopVote = bySignal.FirstOrDefault(p => p.Value == DrumLoop && p.Confidence >= 0.5);
        Label<string>? genericLoop = ordered.FirstOrDefault(label => label.Value == MixedLoop);
        if (drumLoopVote is not null && genericLoop is not null && ordered[0].Value == MixedLoop)
        {
            double refined = 1.0 - ((1.0 - genericLoop.Confidence) * (1.0 - (drumLoopVote.Confidence * SignalWeight)));
            ordered.Insert(0, new Label<string>(DrumLoop, Decibels.Round(refined, 3), [.. genericLoop.Evidence, drumLoopVote.Evidence]));
        }

        Label<string> primary = ordered[0];
        InstrumentConcept primaryConcept = taxonomy.Get(primary.Value);
        List<Label<string>> alternates = [.. ordered.Skip(1).Where(label => label.Confidence >= 0.3 && !AreRelated(taxonomy.Get(label.Value), primaryConcept)).Take(2)];

        string? disagreement = null;
        bool nameIsConfident = byName.Any(p => p.Value == primary.Value && p.Confidence >= DisagreementConfidence);
        ClassificationProposal? topSignal = bySignal.OrderByDescending(p => p.Confidence).FirstOrDefault();
        if (nameIsConfident && topSignal is not null && topSignal.Confidence >= DisagreementConfidence && taxonomy.TryGet(topSignal.Value, out InstrumentConcept? signalConcept))
        {
            bool agrees = AreRelated(primaryConcept, signalConcept) || alternates.Any(label => AreRelated(taxonomy.Get(label.Value), signalConcept));
            bool isWorthFlagging = primaryConcept.IsDistinctive && (signalConcept.IsDistinctive || signalConcept.Category != primaryConcept.Category);
            if (!agrees && isWorthFlagging)
            {
                disagreement = $"the name says {primaryConcept.DisplayName} but the audio looks like {signalConcept.DisplayName} ({topSignal.Confidence:0.00})";
            }
        }

        return (primary, alternates, disagreement, null);
    }

    private static Label<TValue> Manual<TValue>(TValue value, string? note) =>
        new(value, 1.0, [new Evidence(EvidenceSource.Manual, string.IsNullOrWhiteSpace(note) ? "manual override" : note)]);
    #endregion

    #region Public methods
    ///<summary>Combines <paramref name="proposals"/> into the classification of the sample in <paramref name="context"/>.</summary>
    ///<param name="context">The sample and its parsed name.</param>
    ///<param name="proposals">Votes from every classifier.</param>
    ///<param name="manual">A hand-written override for this file, if any.</param>
    public SampleClassification Fuse(ClassificationContext context, IReadOnlyList<ClassificationProposal> proposals, OverrideEntry? manual)
    {
        List<ClassificationProposal> votes = [.. proposals];
        List<string> reasons = [];

        (Label<string> instrument, List<Label<string>> alternates, string? disagreement, string? suggestion) = PickInstrument(votes);
        Label<ContentType> content = PickEnum(votes, ClassificationAxis.ContentType, ContentType.Unknown);
        Label<SoundOrigin> origin = PickEnum(votes, ClassificationAxis.Origin, SoundOrigin.Unknown);
        Label<string>? kit = Pick(votes, ClassificationAxis.Kit);
        Label<string>? style = Pick(votes, ClassificationAxis.Style);

        if (manual is not null)
        {
            string? note = manual.Note;
            if (!string.IsNullOrWhiteSpace(manual.Instrument))
            {
                instrument = Manual(manual.Instrument, note);
                alternates = [];
                disagreement = null;
            }

            if (!string.IsNullOrWhiteSpace(manual.ContentType))
            {
                content = Manual(Enum.Parse<ContentType>(manual.ContentType, ignoreCase: true), note);
            }

            if (!string.IsNullOrWhiteSpace(manual.Origin))
            {
                origin = Manual(Enum.Parse<SoundOrigin>(manual.Origin, ignoreCase: true), note);
            }

            if (!string.IsNullOrWhiteSpace(manual.Kit))
            {
                kit = Manual(manual.Kit, note);
            }

            if (!string.IsNullOrWhiteSpace(manual.Style))
            {
                style = Manual(manual.Style, note);
            }
        }

        bool isManualInstrument = instrument.Evidence.Any(evidence => evidence.Source == EvidenceSource.Manual);

        // A sample labelled by an override or by its audio never went through the name lexicon, so it has no content type
        // yet; every instrument implies one (a riser is a transition, a kick is a one-shot).
        bool needsDefaultContent = content.Value == ContentType.Unknown && Taxonomies.Instruments.TryGet(instrument.Value, out _) && instrument.Value != Unclassified;
        if (needsDefaultContent)
        {
            InstrumentConcept concept = Taxonomies.Instruments.Get(instrument.Value);
            EvidenceSource source = isManualInstrument ? EvidenceSource.Manual : EvidenceSource.Signal;
            content = new Label<ContentType>(concept.DefaultContentType, 0.5, [new Evidence(source, $"default for {concept.DisplayName}")]);
        }

        if (!isManualInstrument)
        {
            if (instrument.Value == Unclassified)
            {
                reasons.Add(suggestion ?? "no evidence for the instrument");
            }
            else if (instrument.Confidence < UncertainBelow)
            {
                reasons.Add($"instrument is uncertain ({instrument.Confidence:0.00})");
            }

            if (disagreement is not null)
            {
                reasons.Add(disagreement);
            }
        }

        return new SampleClassification(
            instrument,
            alternates,
            content,
            origin,
            kit,
            style,
            context.Parsed.Attributes,
            reasons.Count > 0,
            reasons);
    }
    #endregion
}
