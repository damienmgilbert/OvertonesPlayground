using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Ontology.Analysis.Dsp;

///<summary>
///Result of a pitch estimate.
///</summary>
///<param name="FrequencyHz">Estimated fundamental.</param>
///<param name="Confidence">0 - 1; one minus the cumulative-mean-normalized difference at the chosen lag.</param>
///<param name="HarmonicToNoiseDb">10 log10(r / (1 - r)) where r is the normalized autocorrelation at the chosen lag.</param>
public readonly record struct PitchEstimate(double FrequencyHz, double Confidence, double HarmonicToNoiseDb);

///<summary>
///The YIN fundamental-frequency estimator (de Cheveigne and Kawahara, 2002), with the difference function computed by
///FFT-based autocorrelation so a 4096-sample window costs a few FFTs instead of millions of multiplications.
///</summary>
public static class YinPitchDetector
{
    #region Constants
    private const double AbsoluteThreshold = 0.15;
    #endregion

    #region Private methods
    private static int NextPowerOfTwo(int value)
    {
        int power = 1;
        while (power < value)
        {
            power <<= 1;
        }

        return power;
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Estimates the fundamental of <paramref name="window"/>. Returns null when the window is too short or silent.
    ///</summary>
    ///<param name="window">Mono samples; at least 512 long.</param>
    ///<param name="sampleRate">Sample rate of <paramref name="window"/>.</param>
    ///<param name="minHz">Lowest fundamental to consider.</param>
    ///<param name="maxHz">Highest fundamental to consider.</param>
    public static PitchEstimate? Estimate(ReadOnlySpan<float> window, int sampleRate, double minHz = 25.0, double maxHz = 5000.0)
    {
        int n = window.Length;
        bool isTooShort = n < 512;
        if (isTooShort)
        {
            return null;
        }

        int tauMax = Math.Min(n / 2, (int)(sampleRate / minHz));
        int tauMin = Math.Max(2, (int)(sampleRate / maxHz));
        int integration = n - tauMax;
        bool isDegenerate = tauMax <= tauMin + 2 || integration < tauMax;
        if (isDegenerate)
        {
            return null;
        }

        // Prefix sums of x^2 give the two energy terms of the difference function for every lag in O(1).
        double[] prefix = new double[n + 1];
        for (int i = 0; i < n; i++)
        {
            prefix[i + 1] = prefix[i] + ((double)window[i] * window[i]);
        }

        double energyHead = prefix[integration];
        bool isSilent = energyHead < 1e-12;
        if (isSilent)
        {
            return null;
        }

        // acf(tau) = sum_{j < integration} x[j] * x[j + tau], via cross-correlation with FFTs.
        int size = NextPowerOfTwo(n + integration);
        double[] xr = new double[size];
        double[] xi = new double[size];
        double[] ar = new double[size];
        double[] ai = new double[size];
        for (int i = 0; i < n; i++)
        {
            xr[i] = window[i];
        }

        for (int i = 0; i < integration; i++)
        {
            ar[i] = window[i];
        }

        FastFourierTransform.Forward(xr, xi);
        FastFourierTransform.Forward(ar, ai);
        for (int i = 0; i < size; i++)
        {
            double re = (ar[i] * xr[i]) + (ai[i] * xi[i]);
            double im = (ar[i] * xi[i]) - (ai[i] * xr[i]);
            xr[i] = re;
            xi[i] = im;
        }

        // The app's inverse transform already divides by N, so xr[tau] is the correlation itself.
        FastFourierTransform.Inverse(xr, xi);

        double[] difference = new double[tauMax + 1];
        double[] normalized = new double[tauMax + 1];
        for (int tau = 1; tau <= tauMax; tau++)
        {
            double energyShifted = prefix[tau + integration] - prefix[tau];
            difference[tau] = Math.Max(0.0, energyHead + energyShifted - (2.0 * xr[tau]));
        }

        normalized[0] = 1.0;
        double running = 0.0;
        for (int tau = 1; tau <= tauMax; tau++)
        {
            running += difference[tau];
            normalized[tau] = running <= 0 ? 1.0 : difference[tau] * tau / running;
        }

        int best = -1;
        for (int tau = tauMin; tau < tauMax; tau++)
        {
            if (normalized[tau] < AbsoluteThreshold)
            {
                while (tau + 1 < tauMax && normalized[tau + 1] < normalized[tau])
                {
                    tau++;
                }

                best = tau;
                break;
            }
        }

        if (best < 0)
        {
            best = tauMin;
            for (int tau = tauMin + 1; tau < tauMax; tau++)
            {
                if (normalized[tau] < normalized[best])
                {
                    best = tau;
                }
            }
        }

        double refined = best;
        bool canInterpolate = best > 1 && best < tauMax;
        if (canInterpolate)
        {
            double s0 = normalized[best - 1];
            double s1 = normalized[best];
            double s2 = normalized[best + 1];
            double denominator = s0 + s2 - (2.0 * s1);
            bool isCurved = Math.Abs(denominator) > 1e-12;
            if (isCurved)
            {
                refined = best + (0.5 * (s0 - s2) / denominator);
            }
        }

        double confidence = Math.Clamp(1.0 - normalized[best], 0.0, 1.0);
        double energyLag = prefix[best + integration] - prefix[best];
        double correlation = energyHead * energyLag > 0 ? xr[best] / Math.Sqrt(energyHead * energyLag) : 0.0;
        correlation = Math.Clamp(correlation, 0.0, 0.9999);
        double harmonicToNoise = Math.Clamp(10.0 * Math.Log10(correlation / (1.0 - correlation + 1e-12) + 1e-12), -10.0, 40.0);

        return new PitchEstimate(sampleRate / refined, confidence, harmonicToNoise);
    }
    #endregion
}
