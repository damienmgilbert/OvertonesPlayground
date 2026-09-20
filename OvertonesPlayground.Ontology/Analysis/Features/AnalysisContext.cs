namespace OvertonesPlayground.Ontology.Analysis.Features;

///<summary>
///Everything the analyzers of one file share: the decoded audio, a mono mix, envelopes and the active region (each
///computed once, on first use), plus the facets produced so far so later analyzers can build on earlier ones.
///</summary>
public sealed class AnalysisContext
{
    #region Constants
    private const double RmsWindowSeconds = 0.010;
    private const double RmsHopSeconds = 0.005;
    private const double PeakHoldSeconds = 0.020;
    private const double PeakHopSeconds = 0.001;
    #endregion

    #region Fields
    private readonly Lazy<float[]> _mono;
    private readonly Lazy<double> _peak;
    private readonly Lazy<double[]> _rmsEnvelope;
    private readonly Lazy<double[]> _peakEnvelope;
    private readonly Lazy<ActiveRegion> _active;
    #endregion

    #region Constructors
    ///<summary>Creates a context for one decoded file.</summary>
    public AnalysisContext(WavData wav, long fileSizeBytes, NamedAttributes named)
    {
        Wav = wav;
        FileSizeBytes = fileSizeBytes;
        Named = named;

        RmsWindowSamples = Math.Max(2, (int)(RmsWindowSeconds * SampleRate));
        RmsHopSamples = Math.Max(1, (int)(RmsHopSeconds * SampleRate));
        PeakHopSamples = Math.Max(1, (int)(PeakHopSeconds * SampleRate));

        _mono = new Lazy<float[]>(() => Audio.MixToMono());
        _peak = new Lazy<double>(ComputePeak);
        _rmsEnvelope = new Lazy<double[]>(() => EnvelopeTools.RmsEnvelope(Mono, RmsWindowSamples, RmsHopSamples));
        _peakEnvelope = new Lazy<double[]>(() => EnvelopeTools.PeakHoldEnvelope(Audio.Channels, (int)(PeakHoldSeconds * SampleRate), PeakHopSamples));
        _active = new Lazy<ActiveRegion>(() => EnvelopeTools.FindActiveRegion(RmsEnvelope, RmsHopSamples, RmsWindowSamples, Audio.FrameCount));
    }
    #endregion

    #region Private methods
    private double ComputePeak()
    {
        double peak = 0;
        foreach (float[] channel in Audio.Channels)
        {
            foreach (float sample in channel)
            {
                double magnitude = Math.Abs(sample);
                if (magnitude > peak)
                {
                    peak = magnitude;
                }
            }
        }

        return peak;
    }
    #endregion

    #region Public properties
    ///<summary>The audible part of the file.</summary>
    public ActiveRegion Active => _active.Value;

    ///<summary>The decoded audio.</summary>
    public AudioBuffer Audio => Wav.Audio;

    ///<summary>Dynamics facet, once <see cref="DynamicsAnalyzer"/> has run.</summary>
    public DynamicsFacet? Dynamics { get; set; }

    ///<summary>Size of the file on disk.</summary>
    public long FileSizeBytes { get; }

    ///<summary>Storage format.</summary>
    public WavFormat Format => Wav.Format;

    ///<summary>True when the file contains no audible signal.</summary>
    public bool IsSilent => PeakAmplitude < 1e-6;

    ///<summary>Embedded chunks.</summary>
    public RiffMetadata Metadata => Wav.Metadata;

    ///<summary>Average of all channels.</summary>
    public float[] Mono => _mono.Value;

    ///<summary>Tempo, key and other facts written in the file name.</summary>
    public NamedAttributes Named { get; }

    ///<summary>Largest absolute sample over all channels.</summary>
    public double PeakAmplitude => _peak.Value;

    ///<summary>Peak-hold envelope (20 ms hold, 1 ms hop) over all channels.</summary>
    public double[] PeakEnvelope => _peakEnvelope.Value;

    ///<summary>Samples per hop of <see cref="PeakEnvelope"/>.</summary>
    public int PeakHopSamples { get; }

    ///<summary>RMS envelope (10 ms window, 5 ms hop) of the mono mix.</summary>
    public double[] RmsEnvelope => _rmsEnvelope.Value;

    ///<summary>Samples per hop of <see cref="RmsEnvelope"/>.</summary>
    public int RmsHopSamples { get; }

    ///<summary>Samples per window of <see cref="RmsEnvelope"/>.</summary>
    public int RmsWindowSamples { get; }

    ///<summary>Samples per second.</summary>
    public int SampleRate => Audio.SampleRate;

    ///<summary>Spectral facet, once <see cref="SpectralAnalyzer"/> has run.</summary>
    public SpectralFacet? Spectral { get; set; }

    ///<summary>The decoded file with its format and metadata.</summary>
    public WavData Wav { get; }
    #endregion
}
