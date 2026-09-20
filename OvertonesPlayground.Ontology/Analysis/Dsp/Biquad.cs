namespace OvertonesPlayground.Ontology.Analysis.Dsp;

///<summary>
///A second-order IIR filter section (transposed direct form II, double precision) with the designs the analyzers need:
///Butterworth low-pass for band splitting and decimation, and the two ITU-R BS.1770-4 K-weighting stages.
///</summary>
public sealed class Biquad
{
    #region Fields
    private readonly double _b0;
    private readonly double _b1;
    private readonly double _b2;
    private readonly double _a1;
    private readonly double _a2;
    private double _z1;
    private double _z2;
    #endregion

    #region Constructors
    ///<summary>Creates a section from normalized coefficients (a0 = 1).</summary>
    public Biquad(double b0, double b1, double b2, double a1, double a2)
    {
        _b0 = b0;
        _b1 = b1;
        _b2 = b2;
        _a1 = a1;
        _a2 = a2;
    }
    #endregion

    #region Public methods
    ///<summary>ITU-R BS.1770-4 stage 2: the 38 Hz high-pass (RLB weighting), for any sample rate.</summary>
    public static Biquad KWeightingHighPass(double sampleRate)
    {
        const double f0 = 38.13547087602444;
        const double q = 0.5003270373238773;
        double k = Math.Tan(Math.PI * f0 / sampleRate);
        double a0 = 1.0 + (k / q) + (k * k);
        return new Biquad(1.0, -2.0, 1.0, 2.0 * ((k * k) - 1.0) / a0, (1.0 - (k / q) + (k * k)) / a0);
    }

    ///<summary>ITU-R BS.1770-4 stage 1: the +4 dB high shelf around 1.68 kHz (head-related weighting), for any sample rate.</summary>
    public static Biquad KWeightingShelf(double sampleRate)
    {
        const double f0 = 1681.974450955533;
        const double gainDb = 3.999843853973347;
        const double q = 0.7071752369554196;
        double k = Math.Tan(Math.PI * f0 / sampleRate);
        double vh = Math.Pow(10.0, gainDb / 20.0);
        double vb = Math.Pow(vh, 0.4996667741545416);
        double a0 = 1.0 + (k / q) + (k * k);
        return new Biquad(
            (vh + (vb * k / q) + (k * k)) / a0,
            2.0 * ((k * k) - vh) / a0,
            (vh - (vb * k / q) + (k * k)) / a0,
            2.0 * ((k * k) - 1.0) / a0,
            (1.0 - (k / q) + (k * k)) / a0);
    }

    ///<summary>Butterworth-style (Q = 1/sqrt 2) low-pass.</summary>
    public static Biquad LowPass(double sampleRate, double cutoffHz, double q = 0.70710678118)
    {
        double w0 = 2.0 * Math.PI * Math.Min(cutoffHz, sampleRate * 0.49) / sampleRate;
        double cos = Math.Cos(w0);
        double alpha = Math.Sin(w0) / (2.0 * q);
        double a0 = 1.0 + alpha;
        return new Biquad(((1.0 - cos) / 2.0) / a0, (1.0 - cos) / a0, ((1.0 - cos) / 2.0) / a0, -2.0 * cos / a0, (1.0 - alpha) / a0);
    }

    ///<summary>Filters one sample.</summary>
    public double Process(double x)
    {
        double y = (_b0 * x) + _z1;
        _z1 = (_b1 * x) - (_a1 * y) + _z2;
        _z2 = (_b2 * x) - (_a2 * y);
        return y;
    }

    ///<summary>Filters <paramref name="input"/> into a new array, starting from a cleared state.</summary>
    public float[] Process(ReadOnlySpan<float> input)
    {
        Reset();
        float[] output = new float[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            output[i] = (float)Process(input[i]);
        }

        return output;
    }

    ///<summary>Clears the filter memory.</summary>
    public void Reset()
    {
        _z1 = 0;
        _z2 = 0;
    }
    #endregion
}
