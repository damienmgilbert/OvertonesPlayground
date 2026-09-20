namespace OvertonesPlayground.Ontology.Classification;

///<summary>
///Low-weight votes from chunks embedded in the file: a real <c>acid</c> tempo or sampler loop points suggest a loop,
///and a genre tag can name a style. Many files carry sampler defaults, so this evidence is deliberately weak.
///</summary>
public sealed class MetadataClassifier : ISampleClassifier
{
    #region Public methods
    ///<inheritdoc/>
    public IReadOnlyList<ClassificationProposal> Classify(ClassificationContext context)
    {
        List<ClassificationProposal> proposals = [];
        TechnicalFacet? technical = context.Sample.Technical;
        if (technical is null)
        {
            return proposals;
        }

        if (technical.EmbeddedTempoBpm is { } tempo)
        {
            proposals.Add(new ClassificationProposal(
                ClassificationAxis.ContentType,
                ContentType.Loop.ToString(),
                0.55,
                new Evidence(EvidenceSource.Metadata, $"acid chunk holds a tempo of {tempo:0.#} BPM")));
        }

        if (technical.LoopPointCount > 0)
        {
            proposals.Add(new ClassificationProposal(
                ClassificationAxis.ContentType,
                ContentType.Loop.ToString(),
                0.4,
                new Evidence(EvidenceSource.Metadata, $"smpl chunk declares {technical.LoopPointCount} loop point(s)")));
        }

        if (!string.IsNullOrWhiteSpace(technical.EmbeddedGenre))
        {
            string[] words = TextNormalizer.Tokenize(technical.EmbeddedGenre);
            foreach (ConceptAlias<StyleConcept> alias in Taxonomies.Styles.Aliases)
            {
                bool isPresent = alias.Tokens.Length <= words.Length
                    && Enumerable.Range(0, words.Length - alias.Tokens.Length + 1)
                        .Any(start => alias.Tokens.SequenceEqual(words.Skip(start).Take(alias.Tokens.Length)));
                if (isPresent)
                {
                    proposals.Add(new ClassificationProposal(
                        ClassificationAxis.Style,
                        alias.Concept.Key,
                        0.6,
                        new Evidence(EvidenceSource.Metadata, $"genre tag '{technical.EmbeddedGenre}'")));
                    break;
                }
            }
        }

        return proposals;
    }
    #endregion
}
