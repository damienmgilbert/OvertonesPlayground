using System.Security.Cryptography;

namespace OvertonesPlayground.Ontology.Analysis;

///<summary>
///Turns one audio file into a <see cref="Sample"/> with every acoustic facet filled in. Classification is left as a
///placeholder: run <see cref="CorpusClassifier"/> over the whole corpus afterwards, because the signal classifier learns
///from the other files. A failure in one analyzer never loses the others; it is recorded in the sample's notes.
///</summary>
public sealed class SampleAnalysisPipeline
{
    #region Fields
    private readonly TechnicalAnalyzer _technical = new();
    private readonly DynamicsAnalyzer _dynamics = new();
    private readonly SpectralAnalyzer _spectral = new();
    private readonly TonalityAnalyzer _tonality = new();
    private readonly StereoAnalyzer _stereo = new();
    private readonly RhythmAnalyzer _rhythm = new();
    #endregion

    #region Private methods
    private static Sample Unreadable(SampleAsset asset, SampleClassification placeholder, AnalysisStatus status, string note) =>
        new(asset, status, null, null, null, null, null, null, placeholder, [note]);
    #endregion

    #region Public methods
    ///<summary>Analyses the file at <paramref name="path"/>.</summary>
    public Sample AnalyzeFile(string path) => AnalyzeBytes(Path.GetFileName(path), File.ReadAllBytes(path));

    ///<summary>Analyses a file that is already in memory.</summary>
    ///<param name="fileName">File name with extension; becomes the sample id.</param>
    ///<param name="bytes">The whole file.</param>
    public Sample AnalyzeBytes(string fileName, byte[] bytes)
    {
        SampleAsset asset = new(fileName, bytes.LongLength, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        ParsedName parsed = FilenameParser.Parse(Path.GetFileNameWithoutExtension(fileName));
        SampleClassification placeholder = SampleClassification.Placeholder(parsed.Attributes.Stem);

        if (!RiffWavReader.IsRiffWave(bytes))
        {
            return Unreadable(asset, placeholder, AnalysisStatus.UnsupportedFormat, "not a RIFF/WAVE file; only the file name can be used as evidence");
        }

        WavData wav;
        try
        {
            wav = RiffWavReader.Read(bytes);
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidDataException)
        {
            return Unreadable(asset, placeholder, AnalysisStatus.Failed, $"could not decode: {ex.Message}");
        }

        AnalysisContext context = new(wav, bytes.LongLength, parsed.Attributes);
        List<string> notes = [];
        int failures = 0;

        T? Run<T>(string name, Func<T?> extract)
            where T : class
        {
            try
            {
                return extract();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                failures++;
                notes.Add($"{name} analysis failed: {ex.Message}");
                return null;
            }
        }

        TechnicalFacet? technical = Run("technical", () => _technical.Extract(context));
        DynamicsFacet? dynamics = Run("dynamics", () => _dynamics.Extract(context));
        context.Dynamics = dynamics;
        SpectralFacet? spectral = Run("spectral", () => _spectral.Extract(context));
        context.Spectral = spectral;
        TonalityFacet? tonality = Run("tonality", () => _tonality.Extract(context));
        StereoFacet? stereo = Run("stereo", () => _stereo.Extract(context));
        RhythmFacet? rhythm = Run("rhythm", () => _rhythm.Extract(context));

        if (wav.Format.WasTruncated)
        {
            notes.Add($"the data chunk declared more audio than the file holds; analysed the {wav.Audio.DurationSeconds:0.###} s that is present");
        }

        if (context.IsSilent)
        {
            notes.Add("the file is digitally silent");
        }

        return new Sample(
            asset,
            failures > 0 ? AnalysisStatus.Partial : AnalysisStatus.Analyzed,
            technical,
            spectral,
            tonality,
            dynamics,
            stereo,
            rhythm,
            placeholder,
            notes.Count == 0 ? null : notes);
    }
    #endregion
}
