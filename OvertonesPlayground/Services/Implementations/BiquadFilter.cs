using OvertonesPlayground.Models;

namespace OvertonesPlayground.Services.Implementations;

///<summary>
///A standard RBJ-cookbook biquad filter (low-pass/high-pass/band-pass), applied in place over a 16-bit PCM buffer as a
///post-processing step for synthesized sounds.
///</summary>
internal static class BiquadFilter
{
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
    #endregion
}
