namespace OvertonesPlayground.Ontology.Analysis.Features;

///<summary>
///Stereo field and phase: correlation, mid/side width, balance, pan, mono compatibility and low-band phase.
///</summary>
public sealed class StereoAnalyzer : IFeatureExtractor<StereoFacet>
{
    #region Constants
    private const double DualMonoTolerance = 1e-6;
    private const double LowBandHz = 150.0;
    private const double NarrowCorrelation = 0.7;
    private const double OutOfPhaseCorrelation = -0.3;
    #endregion

    #region Private methods
    private static double Correlation(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        double ll = 0;
        double rr = 0;
        double lr = 0;
        for (int i = 0; i < left.Length; i++)
        {
            ll += (double)left[i] * left[i];
            rr += (double)right[i] * right[i];
            lr += (double)left[i] * right[i];
        }

        double denominator = Math.Sqrt(ll * rr);
        return denominator <= 1e-18 ? 1.0 : Math.Clamp(lr / denominator, -1.0, 1.0);
    }

    private static float[] LowPass(ReadOnlySpan<float> input, int sampleRate)
    {
        Biquad first = Biquad.LowPass(sampleRate, LowBandHz);
        Biquad second = Biquad.LowPass(sampleRate, LowBandHz);
        return second.Process(first.Process(input));
    }

    private static StereoFacet MonoFacet(int channels) => new(channels, StereoImage.Mono, 1, 1, 0, 0, 0, 0, false);
    #endregion

    #region Public methods
    ///<inheritdoc/>
    public StereoFacet Extract(AnalysisContext context)
    {
        int channelCount = context.Audio.ChannelCount;
        if (channelCount < 2 || context.IsSilent)
        {
            return MonoFacet(channelCount);
        }

        float[] left = context.Audio.Channels[0];
        float[] right = context.Audio.Channels[1];

        bool isIdentical = true;
        for (int i = 0; i < left.Length; i++)
        {
            if (Math.Abs(left[i] - right[i]) > DualMonoTolerance)
            {
                isIdentical = false;
                break;
            }
        }

        if (isIdentical)
        {
            return new StereoFacet(channelCount, StereoImage.DualMono, 1, 1, 0, 0, 0, 0, false);
        }

        ActiveRegion active = context.Active;
        int from = active.StartFrame;
        int length = Math.Max(1, active.Length);
        ReadOnlySpan<float> l = left.AsSpan(from, length);
        ReadOnlySpan<float> r = right.AsSpan(from, length);

        double ll = 0;
        double rr = 0;
        double mm = 0;
        double ss = 0;
        for (int i = 0; i < l.Length; i++)
        {
            double a = l[i];
            double b = r[i];
            ll += a * a;
            rr += b * b;
            double mid = (a + b) / 2.0;
            double side = (a - b) / 2.0;
            mm += mid * mid;
            ss += side * side;
        }

        double correlation = Correlation(l, r);
        double lowCorrelation = Correlation(LowPass(l, context.SampleRate), LowPass(r, context.SampleRate));
        double width = mm + ss <= 1e-18 ? 0 : ss / (mm + ss);
        double balanceDb = Math.Clamp(10.0 * Math.Log10((ll + 1e-18) / (rr + 1e-18)), -60, 60);
        double sqrtL = Math.Sqrt(ll);
        double sqrtR = Math.Sqrt(rr);
        double pan = sqrtL + sqrtR <= 1e-12 ? 0 : (sqrtR - sqrtL) / (sqrtL + sqrtR);
        double monoDb = Math.Clamp(10.0 * Math.Log10((mm + 1e-18) / (((ll + rr) / 2.0) + 1e-18)), -60, 0);
        bool isInverted = correlation < OutOfPhaseCorrelation;

        StereoImage image = isInverted
            ? StereoImage.OutOfPhase
            : correlation >= NarrowCorrelation ? StereoImage.Narrow : StereoImage.Wide;

        return new StereoFacet(
            channelCount,
            image,
            Decibels.Round(correlation, 3),
            Decibels.Round(lowCorrelation, 3),
            Decibels.Round(width, 3),
            Decibels.Round(balanceDb, 2),
            Decibels.Round(pan, 3),
            Decibels.Round(monoDb, 2),
            isInverted);
    }
    #endregion
}
