namespace OvertonesPlayground.Ontology.Querying;

///<summary>
///A standardized feature space over a set of samples, used to answer "what sounds like this?". Each sample becomes a
///fixed-length vector of the acoustic facets that describe <em>what the sound is</em> (brightness, spectrum shape, band
///energies, envelope, pitchedness, rhythm); level and stereo width are left out on purpose because they describe how it
///was mixed, not what it is. Every feature is z-scored against the corpus so no single unit dominates the distance.
///</summary>
public sealed class FingerprintSpace
{
    #region Constants
    ///<summary>Number of features per vector.</summary>
    public const int Dimensions = 34;

    ///<summary>Names of the features, in vector order.</summary>
    public static readonly IReadOnlyList<string> FeatureNames =
    [
        "log centroid", "log rolloff", "log bandwidth", "flatness", "flux", "tilt",
        "sub", "bass", "low-mid", "mid", "high-mid", "presence", "air",
        "crest", "log attack", "log decay", "log duration", "pitch confidence", "harmonicity", "log onset rate", "log active length",
        "log onset centroid",
        "mfcc 1", "mfcc 2", "mfcc 3", "mfcc 4", "mfcc 5", "mfcc 6", "mfcc 7", "mfcc 8", "mfcc 9", "mfcc 10", "mfcc 11", "mfcc 12",
    ];
    #endregion

    #region Fields
    private readonly double[] _mean = new double[Dimensions];
    private readonly double[] _scale = new double[Dimensions];
    private readonly Dictionary<string, double[]> _vectors = new(StringComparer.Ordinal);
    #endregion

    #region Constructors
    ///<summary>Learns the mean and spread of each feature from <paramref name="samples"/> and vectorizes them.</summary>
    public FingerprintSpace(IEnumerable<Sample> samples)
    {
        List<(string Id, double[] Raw)> raws = [];
        foreach (Sample sample in samples)
        {
            double[]? raw = Raw(sample);
            if (raw is not null)
            {
                raws.Add((sample.Id, raw));
            }
        }

        for (int d = 0; d < Dimensions; d++)
        {
            double sum = 0;
            foreach ((string _, double[] raw) in raws)
            {
                sum += raw[d];
            }

            double mean = raws.Count == 0 ? 0 : sum / raws.Count;
            double variance = 0;
            foreach ((string _, double[] raw) in raws)
            {
                variance += (raw[d] - mean) * (raw[d] - mean);
            }

            double deviation = raws.Count == 0 ? 1 : Math.Sqrt(variance / raws.Count);
            _mean[d] = mean;
            _scale[d] = deviation < 1e-9 ? 1 : deviation;
        }

        foreach ((string id, double[] raw) in raws)
        {
            _vectors[id] = Standardize(raw);
        }
    }
    #endregion

    #region Private methods
    private double[] Standardize(double[] raw)
    {
        double[] vector = new double[Dimensions];
        for (int d = 0; d < Dimensions; d++)
        {
            vector[d] = (raw[d] - _mean[d]) / _scale[d];
        }

        return vector;
    }

    private static double Log10(double value) => Math.Log10(Math.Max(value, 0) + 1.0);

    ///<summary>Unstandardized features of <paramref name="sample"/>, or null when it has no spectral or dynamics facet.</summary>
    internal static double[]? Raw(Sample sample)
    {
        SpectralFacet? spectral = sample.Spectral;
        DynamicsFacet? dynamics = sample.Dynamics;
        TechnicalFacet? technical = sample.Technical;
        if (spectral is null || dynamics is null || technical is null)
        {
            return null;
        }

        double[] bands = spectral.Bands.ToArray();
        double[] mfcc = spectral.Mfcc.Length == 12 ? spectral.Mfcc : new double[12];
        return
        [
            Log10(spectral.CentroidHz),
            Log10(spectral.RolloffHz),
            Log10(spectral.BandwidthHz),
            spectral.Flatness,
            Math.Min(spectral.Flux, 2.0),
            Math.Clamp(spectral.TiltDbPerOctave, -24, 6),
            Math.Sqrt(bands[0]), Math.Sqrt(bands[1]), Math.Sqrt(bands[2]), Math.Sqrt(bands[3]), Math.Sqrt(bands[4]), Math.Sqrt(bands[5]), Math.Sqrt(bands[6]),
            Math.Clamp(dynamics.CrestDb, 0, 40),
            Log10(dynamics.AttackMs),
            Log10(dynamics.DecayMs),
            Math.Log10(technical.DurationSeconds + 0.01),
            sample.Tonality?.PitchConfidence ?? 0,
            Math.Clamp(sample.Tonality?.HarmonicToNoiseDb ?? -10, -10, 40),
            Math.Log(1 + (sample.Rhythm?.OnsetsPerSecond ?? 0)),
            Math.Log10(dynamics.ActiveSeconds + 0.01),
            Log10(spectral.OnsetCentroidHz),
            mfcc[0], mfcc[1], mfcc[2], mfcc[3], mfcc[4], mfcc[5], mfcc[6], mfcc[7], mfcc[8], mfcc[9], mfcc[10], mfcc[11],
        ];
    }
    #endregion

    #region Public methods
    ///<summary>Euclidean distance between two standardized vectors.</summary>
    public static double Distance(double[] a, double[] b)
    {
        double sum = 0;
        for (int d = 0; d < Dimensions; d++)
        {
            double delta = a[d] - b[d];
            sum += delta * delta;
        }

        return Math.Sqrt(sum);
    }

    ///<summary>Standardized vector of <paramref name="sample"/>, or null when it has none.</summary>
    public double[]? Vector(Sample sample)
    {
        if (_vectors.TryGetValue(sample.Id, out double[]? cached))
        {
            return cached;
        }

        double[]? raw = Raw(sample);
        return raw is null ? null : Standardize(raw);
    }
    #endregion
}
