using OvertonesPlayground.Models;

namespace OvertonesPlayground.Services.Implementations;

///<summary>
///A standard RBJ-cookbook biquad filter (low-pass/high-pass/band-pass/peaking/shelf), applied over a 16-bit PCM
///buffer. <see cref="Apply"/> is the original mono/flat-buffer form used to post-process synthesized sounds;
///<see cref="ApplyLowShelf"/>/<see cref="ApplyPeaking"/>/<see cref="ApplyHighShelf"/> are channel-aware (independent
///filter state per channel) for the equalizer, which runs over arbitrary-channel recorded/imported clips.
///</summary>
internal static class BiquadFilter
{
    #region Constants
    ///<summary>
    ///Equalizer bands use the RBJ cookbook's shelf/peaking "A" parameter, the square root of the linear gain.
    ///</summary>
    private const double ShelfPeakGainExponentDivisor = 40.0;
    #endregion

    #region Private methods
    ///<summary>
    ///Applies a peaking or shelving filter across every channel of an interleaved buffer, with independent filter
    ///state per channel so a stereo signal's left and right history never mix.
    ///</summary>
    private static void ApplyShelfOrPeak(short[] samples, int channels, int sampleRate, ShelfPeakKind kind, double frequencyHz, double gainDb, double q)
    {
        bool isEmpty = samples.Length == 0 || channels <= 0;
        if (isEmpty)
        {
            return;
        }

        double nyquist = sampleRate / 2.0;
        frequencyHz = Math.Clamp(frequencyHz, 20, nyquist - 1);
        double a = Math.Pow(10, gainDb / ShelfPeakGainExponentDivisor);
        double omega = 2 * Math.PI * frequencyHz / sampleRate;
        double sinw = Math.Sin(omega);
        double cosw = Math.Cos(omega);
        double alpha = sinw / (2 * Math.Max(0.1, q));
        double twoSqrtAAlpha = 2 * Math.Sqrt(a) * alpha;

        double b0, b1, b2, a0, a1, a2;
        switch (kind)
        {
            case ShelfPeakKind.Peaking:
                b0 = 1 + (alpha * a);
                b1 = -2 * cosw;
                b2 = 1 - (alpha * a);
                a0 = 1 + (alpha / a);
                a1 = -2 * cosw;
                a2 = 1 - (alpha / a);
                break;

            case ShelfPeakKind.LowShelf:
                b0 = a * (a + 1 - ((a - 1) * cosw) + twoSqrtAAlpha);
                b1 = 2 * a * (a - 1 - ((a + 1) * cosw));
                b2 = a * (a + 1 - ((a - 1) * cosw) - twoSqrtAAlpha);
                a0 = a + 1 + ((a - 1) * cosw) + twoSqrtAAlpha;
                a1 = -2 * (a - 1 + ((a + 1) * cosw));
                a2 = a + 1 + ((a - 1) * cosw) - twoSqrtAAlpha;
                break;

            case ShelfPeakKind.HighShelf:
                b0 = a * (a + 1 + ((a - 1) * cosw) + twoSqrtAAlpha);
                b1 = -2 * a * (a - 1 + ((a + 1) * cosw));
                b2 = a * (a + 1 + ((a - 1) * cosw) - twoSqrtAAlpha);
                a0 = a + 1 - ((a - 1) * cosw) + twoSqrtAAlpha;
                a1 = 2 * (a - 1 - ((a + 1) * cosw));
                a2 = a + 1 - ((a - 1) * cosw) - twoSqrtAAlpha;
                break;

            default:
                return;
        }

        b0 /= a0;
        b1 /= a0;
        b2 /= a0;
        a1 /= a0;
        a2 /= a0;

        double[] x1 = new double[channels];
        double[] x2 = new double[channels];
        double[] y1 = new double[channels];
        double[] y2 = new double[channels];

        int frames = samples.Length / channels;
        for (int frame = 0; frame < frames; frame++)
        {
            for (int channel = 0; channel < channels; channel++)
            {
                int index = (frame * channels) + channel;
                double x0 = samples[index] / (double)short.MaxValue;
                double y0 = (b0 * x0) + (b1 * x1[channel]) + (b2 * x2[channel]) - (a1 * y1[channel]) - (a2 * y2[channel]);

                x2[channel] = x1[channel];
                x1[channel] = x0;
                y2[channel] = y1[channel];
                y1[channel] = y0;

                samples[index] = WaveformGenerator.ToShort(y0);
            }
        }
    }
    #endregion

    #region Private types
    private enum ShelfPeakKind { LowShelf, Peaking, HighShelf }
    #endregion

    #region Public methods
    public static void Apply(short[] samples, int sampleRate, FilterType type, double cutoffHz, double resonanceQ)
    {
        bool isNone = type == FilterType.None;
        bool isEmpty = samples.Length == 0;
        if (isNone || isEmpty)
        {
            return;
        }

        double nyquist = sampleRate / 2.0;
        cutoffHz = Math.Clamp(cutoffHz, 20, nyquist - 1);
        double q = Math.Max(0.1, resonanceQ);

        double omega = 2 * Math.PI * cutoffHz / sampleRate;
        double alpha = Math.Sin(omega) / (2 * q);
        double cosw = Math.Cos(omega);

        double b0, b1, b2, a0, a1, a2;

        switch (type)
        {
            case FilterType.LowPass:
                b0 = (1 - cosw) / 2;
                b1 = 1 - cosw;
                b2 = (1 - cosw) / 2;
                a0 = 1 + alpha;
                a1 = -2 * cosw;
                a2 = 1 - alpha;
                break;

            case FilterType.HighPass:
                b0 = (1 + cosw) / 2;
                b1 = -(1 + cosw);
                b2 = (1 + cosw) / 2;
                a0 = 1 + alpha;
                a1 = -2 * cosw;
                a2 = 1 - alpha;
                break;

            case FilterType.BandPass:
                b0 = alpha;
                b1 = 0;
                b2 = -alpha;
                a0 = 1 + alpha;
                a1 = -2 * cosw;
                a2 = 1 - alpha;
                break;

            default:
                return;
        }

        b0 /= a0;
        b1 /= a0;
        b2 /= a0;
        a1 /= a0;
        a2 /= a0;

        double x1 = 0, x2 = 0, y1 = 0, y2 = 0;

        for (int i = 0; i < samples.Length; i++)
        {
            double x0 = samples[i] / (double)short.MaxValue;
            double y0 = (b0 * x0) + (b1 * x1) + (b2 * x2) - (a1 * y1) - (a2 * y2);

            x2 = x1;
            x1 = x0;
            y2 = y1;
            y1 = y0;

            samples[i] = WaveformGenerator.ToShort(y0);
        }
    }

    ///<summary>
    ///Boosts or cuts frequencies above <paramref name="frequencyHz"/>, per channel.
    ///</summary>
    public static void ApplyHighShelf(short[] samples, int channels, int sampleRate, double frequencyHz, double gainDb, double q) =>
        ApplyShelfOrPeak(samples, channels, sampleRate, ShelfPeakKind.HighShelf, frequencyHz, gainDb, q);

    ///<summary>
    ///Boosts or cuts frequencies below <paramref name="frequencyHz"/>, per channel.
    ///</summary>
    public static void ApplyLowShelf(short[] samples, int channels, int sampleRate, double frequencyHz, double gainDb, double q) =>
        ApplyShelfOrPeak(samples, channels, sampleRate, ShelfPeakKind.LowShelf, frequencyHz, gainDb, q);

    ///<summary>
    ///Boosts or cuts a band centered on <paramref name="frequencyHz"/>, per channel.
    ///</summary>
    public static void ApplyPeaking(short[] samples, int channels, int sampleRate, double frequencyHz, double gainDb, double q) =>
        ApplyShelfOrPeak(samples, channels, sampleRate, ShelfPeakKind.Peaking, frequencyHz, gainDb, q);
    #endregion
}
