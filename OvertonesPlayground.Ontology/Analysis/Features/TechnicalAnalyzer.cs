namespace OvertonesPlayground.Ontology.Analysis.Features;

///<summary>
///Reports how the file is stored.
///</summary>
public sealed class TechnicalAnalyzer : IFeatureExtractor<TechnicalFacet>
{
    #region Public methods
    ///<inheritdoc/>
    public TechnicalFacet Extract(AnalysisContext context) =>
        new(
            context.SampleRate,
            context.Format.BitsPerSample,
            context.Format.Channels,
            Decibels.Round(context.Audio.DurationSeconds, 4),
            context.FileSizeBytes,
            context.Format.EncodingName,
            context.Format.WasTruncated,
            context.Metadata.SampleLoopCount,
            context.Metadata.AcidIsOneShot == false ? context.Metadata.AcidTempo : null,
            context.Metadata.InfoTags.GetValueOrDefault("IGNR"));
    #endregion
}
