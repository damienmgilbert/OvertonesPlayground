namespace OvertonesPlayground.Ontology.Analysis.Features;

///<summary>
///Amplitude and loudness: sample and true peak, RMS, ITU-R BS.1770-4 loudness, loudness range, crest factor, DC offset,
///clipping, lead / trail silence, attack, decay and the envelope archetype.
///</summary>
public sealed class DynamicsAnalyzer : IFeatureExtractor<DynamicsFacet>
{
    #region Constants
    private const double AbsoluteGateLufs = -70.0;
    private const double ClipThreshold = 0.98;
    private const double ClipFlatness = 2e-6;
    private const int ClipRunLength = 3;
    private const double LoudnessOffset = -0.691;
    private const double ShortSoundSeconds = 3.0;
    private const int TruePeakTaps = 8;

    private static readonly double[][] _truePeakKernels = BuildTruePeakKernels();
    #endregion

    #region Private methods
    private static double[][] BuildTruePeakKernels()
    {
        double[] fractions = [0.25, 0.5, 0.75];
        double[][] kernels = new double[fractions.Length][];
        for (int f = 0; f < fractions.Length; f++)
        {
            double[] kernel = new double[2 * TruePeakTaps];
            for (int t = 0; t < kernel.Length; t++)
            {
                double distance = t - (TruePeakTaps - 1) - fractions[f];
                double sinc = Math.Abs(distance) < 1e-9 ? 1.0 : Math.Sin(Math.PI * distance) / (Math.PI * distance);
                double window = 0.5 + (0.5 * Math.Cos(Math.PI * distance / TruePeakTaps));
                kernel[t] = sinc * window;
            }

            kernels[f] = kernel;
        }

        return kernels;
    }

    ///<summary>Largest value of the 4x oversampled signal, checked only around samples near the sample peak.</summary>
    private static double TruePeak(float[][] channels, double samplePeak)
    {
        double truePeak = samplePeak;
        double candidateThreshold = samplePeak * 0.7;
        foreach (float[] channel in channels)
        {
            for (int i = 0; i < channel.Length; i++)
            {
                if (Math.Abs(channel[i]) < candidateThreshold)
                {
                    continue;
                }

                foreach (double[] kernel in _truePeakKernels)
                {
                    double sum = 0;
                    for (int t = 0; t < kernel.Length; t++)
                    {
                        int index = i + t - (TruePeakTaps - 1);
                        bool isInside = index >= 0 && index < channel.Length;
                        if (isInside)
                        {
                            sum += channel[index] * kernel[t];
                        }
                    }

                    truePeak = Math.Max(truePeak, Math.Abs(sum));
                }
            }
        }

        return truePeak;
    }

    ///<summary>
    ///Counts samples inside flat tops: runs of at least three consecutive samples that sit near full scale and are
    ///identical to each other. A genuine full-scale sine also spends a few samples near its crest, but those keep
    ///changing, so they are not counted.
    ///</summary>
    private static int CountClippedSamples(float[][] channels)
    {
        int clipped = 0;
        foreach (float[] channel in channels)
        {
            int run = 0;
            for (int i = 0; i <= channel.Length; i++)
            {
                bool isNearFullScale = i < channel.Length && Math.Abs(channel[i]) >= ClipThreshold;
                bool continuesRun = isNearFullScale && run > 0 && Math.Abs(channel[i] - channel[i - 1]) <= ClipFlatness;
                if (continuesRun)
                {
                    run++;
                    continue;
                }

                if (run >= ClipRunLength)
                {
                    clipped += run;
                }

                run = isNearFullScale ? 1 : 0;
            }
        }

        return clipped;
    }

    ///<summary>Sum over channels of K-weighted energy in consecutive segments of <paramref name="segmentFrames"/> frames.</summary>
    private static double[] KWeightedSegmentEnergy(float[][] channels, int sampleRate, int segmentFrames, int fromFrame, int toFrame)
    {
        int segments = Math.Max(1, (toFrame - fromFrame + segmentFrames - 1) / segmentFrames);
        double[] energy = new double[segments];
        foreach (float[] channel in channels)
        {
            float[] weighted = Biquad.KWeightingHighPass(sampleRate).Process(Biquad.KWeightingShelf(sampleRate).Process(channel.AsSpan(fromFrame, toFrame - fromFrame)));
            for (int i = 0; i < weighted.Length; i++)
            {
                energy[i / segmentFrames] += (double)weighted[i] * weighted[i];
            }
        }

        return energy;
    }

    private static double Percentile(List<double> sorted, double fraction)
    {
        double position = fraction * (sorted.Count - 1);
        int lower = (int)Math.Floor(position);
        int upper = Math.Min(sorted.Count - 1, lower + 1);
        return sorted[lower] + ((sorted[upper] - sorted[lower]) * (position - lower));
    }

    ///<summary>Integrated loudness (BS.1770-4) and loudness range (EBU R128).</summary>
    private static (double Lufs, bool IsGated, double RangeLu) Loudness(AnalysisContext context)
    {
        float[][] channels = context.Audio.Channels;
        int sampleRate = context.SampleRate;
        int frames = context.Audio.FrameCount;
        ActiveRegion active = context.Active;

        double activeSeconds = (double)active.Length / sampleRate;
        bool isShort = activeSeconds < ShortSoundSeconds;
        if (isShort)
        {
            double[] energy = KWeightedSegmentEnergy(channels, sampleRate, Math.Max(1, active.Length), active.StartFrame, active.EndFrame);
            double meanSquare = energy[0] / Math.Max(1, active.Length);
            return (Decibels.Round(Math.Max(Decibels.Floor, LoudnessOffset + Decibels.FromPower(meanSquare)), 2), false, 0.0);
        }

        int step = Math.Max(1, sampleRate / 10);
        double[] segments = KWeightedSegmentEnergy(channels, sampleRate, step, 0, frames);
        int blockSegments = 4;
        List<double> blocks = [];
        for (int s = 0; s + blockSegments <= segments.Length; s++)
        {
            double sum = 0;
            for (int j = 0; j < blockSegments; j++)
            {
                sum += segments[s + j];
            }

            blocks.Add(sum / (blockSegments * step));
        }

        List<double> aboveAbsolute = [.. blocks.Where(power => LoudnessOffset + Decibels.FromPower(power) > AbsoluteGateLufs)];
        if (aboveAbsolute.Count == 0)
        {
            double[] whole = KWeightedSegmentEnergy(channels, sampleRate, Math.Max(1, active.Length), active.StartFrame, active.EndFrame);
            return (Decibels.Round(LoudnessOffset + Decibels.FromPower(whole[0] / Math.Max(1, active.Length)), 2), false, 0.0);
        }

        double relativeGate = LoudnessOffset + Decibels.FromPower(aboveAbsolute.Average()) - 10.0;
        List<double> gated = [.. aboveAbsolute.Where(power => LoudnessOffset + Decibels.FromPower(power) > relativeGate)];
        double integrated = gated.Count == 0 ? relativeGate + 10.0 : LoudnessOffset + Decibels.FromPower(gated.Average());

        double range = 0.0;
        int shortTermSegments = 30;
        int shortTermStep = 10;
        List<double> shortTerm = [];
        for (int s = 0; s + shortTermSegments <= segments.Length; s += shortTermStep)
        {
            double sum = 0;
            for (int j = 0; j < shortTermSegments; j++)
            {
                sum += segments[s + j];
            }

            shortTerm.Add(sum / (shortTermSegments * step));
        }

        List<double> shortAbove = [.. shortTerm.Where(power => LoudnessOffset + Decibels.FromPower(power) > AbsoluteGateLufs)];
        if (shortAbove.Count > 1)
        {
            double shortGate = LoudnessOffset + Decibels.FromPower(shortAbove.Average()) - 20.0;
            List<double> values = [.. shortAbove.Select(power => LoudnessOffset + Decibels.FromPower(power)).Where(lufs => lufs > shortGate).Order()];
            if (values.Count > 1)
            {
                range = Percentile(values, 0.95) - Percentile(values, 0.10);
            }
        }

        return (Decibels.Round(integrated, 2), true, Decibels.Round(range, 2));
    }

    private static double MeanSquare(float[][] channels, int from, int to)
    {
        double sum = 0;
        long count = 0;
        foreach (float[] channel in channels)
        {
            for (int i = from; i < to; i++)
            {
                sum += (double)channel[i] * channel[i];
            }

            count += Math.Max(0, to - from);
        }

        return count == 0 ? 0.0 : sum / count;
    }

    private static DynamicsFacet Silent(AnalysisContext context) =>
        new(Decibels.Floor, Decibels.Floor, Decibels.Floor, Decibels.Floor, Decibels.Floor, false, 0, 0, 0, 0, Decibels.Round(context.Audio.DurationSeconds * 1000, 1), 0, 0, 0, 0, EnvelopeShape.Impulsive);

    ///<summary>Attack, decay and the archetype of the peak-hold envelope.</summary>
    private static (double AttackMs, double DecayMs, EnvelopeShape Shape) Envelope(AnalysisContext context)
    {
        double[] env = context.PeakEnvelope;
        double hopMs = 1000.0 * context.PeakHopSamples / context.SampleRate;
        int peakIndex = 0;
        for (int i = 1; i < env.Length; i++)
        {
            if (env[i] > env[peakIndex] * 1.0000001)
            {
                peakIndex = i;
            }
        }

        double peak = env[peakIndex];
        int t10 = 0;
        while (t10 < env.Length && env[t10] < peak * 0.1)
        {
            t10++;
        }

        int t90 = t10;
        while (t90 < env.Length && env[t90] < peak * 0.9)
        {
            t90++;
        }

        double attackMs = Math.Max(0, t90 - t10) * hopMs;

        double fall30 = peak * Decibels.ToAmplitude(-30.0);
        int decayIndex = peakIndex;
        while (decayIndex < env.Length - 1 && env[decayIndex] >= fall30)
        {
            decayIndex++;
        }

        double decayMs = (decayIndex - peakIndex) * hopMs;

        ActiveRegion active = context.Active;
        double activeMs = 1000.0 * active.Length / context.SampleRate;
        int firstHop = active.StartFrame / context.PeakHopSamples;
        int lastHop = Math.Min(env.Length - 1, active.EndFrame / context.PeakHopSamples);
        int spanHops = Math.Max(1, lastHop - firstHop + 1);
        double peakFraction = Math.Clamp((double)(peakIndex - firstHop) / spanHops, 0.0, 1.0);

        double sustainThreshold = peak * Decibels.ToAmplitude(-12.0);
        int sustained = 0;
        for (int i = firstHop; i <= lastHop; i++)
        {
            if (env[i] >= sustainThreshold)
            {
                sustained++;
            }
        }

        double sustainRatio = (double)sustained / spanHops;

        int lastLoud = lastHop;
        double halfLevel = peak * 0.5;
        while (lastLoud > firstHop && env[lastLoud] < halfLevel)
        {
            lastLoud--;
        }

        int tailEnd = lastLoud;
        while (tailEnd < lastHop && env[tailEnd] >= fall30)
        {
            tailEnd++;
        }

        double tailFallMs = (tailEnd - lastLoud) * hopMs;

        EnvelopeShape shape;
        if (activeMs < 20)
        {
            shape = EnvelopeShape.Impulsive;
        }
        else if (peakFraction >= 0.7 && attackMs >= 50 && attackMs >= 0.1 * activeMs)
        {
            shape = EnvelopeShape.Reverse;
        }
        else if (attackMs >= 100 && peakFraction >= 0.2)
        {
            shape = EnvelopeShape.Swell;
        }
        else if (sustainRatio >= 0.55)
        {
            shape = tailFallMs < 30 && activeMs >= 100 ? EnvelopeShape.Gated : EnvelopeShape.Sustained;
        }
        else
        {
            shape = decayMs <= 200 ? EnvelopeShape.Impulsive : EnvelopeShape.Plucked;
        }

        return (Decibels.Round(attackMs, 1), Decibels.Round(decayMs, 1), shape);
    }
    #endregion

    #region Public methods
    ///<inheritdoc/>
    public DynamicsFacet Extract(AnalysisContext context)
    {
        if (context.IsSilent)
        {
            return Silent(context);
        }

        float[][] channels = context.Audio.Channels;
        int frames = context.Audio.FrameCount;
        int sampleRate = context.SampleRate;
        ActiveRegion active = context.Active;

        double peak = context.PeakAmplitude;
        double peakDb = Decibels.FromAmplitude(peak);
        double truePeakDb = Math.Max(peakDb, Decibels.FromAmplitude(TruePeak(channels, peak)));
        double rmsDb = Decibels.FromPower(MeanSquare(channels, 0, frames));
        double activeRmsDb = Decibels.FromPower(MeanSquare(channels, active.StartFrame, active.EndFrame));

        (double lufs, bool isGated, double range) = Loudness(context);

        double dcSum = 0;
        foreach (float[] channel in channels)
        {
            double sum = 0;
            foreach (float sample in channel)
            {
                sum += sample;
            }

            dcSum += sum / Math.Max(1, channel.Length);
        }

        double dcOffset = dcSum / channels.Length;

        double silenceThreshold = peak * 0.001;
        int first = -1;
        int last = -1;
        for (int i = 0; i < frames && first < 0; i++)
        {
            foreach (float[] channel in channels)
            {
                if (Math.Abs(channel[i]) > silenceThreshold)
                {
                    first = i;
                    break;
                }
            }
        }

        for (int i = frames - 1; i >= 0 && last < 0; i--)
        {
            foreach (float[] channel in channels)
            {
                if (Math.Abs(channel[i]) > silenceThreshold)
                {
                    last = i;
                    break;
                }
            }
        }

        double leadMs = first < 0 ? 0 : 1000.0 * first / sampleRate;
        double trailMs = last < 0 ? 0 : 1000.0 * (frames - 1 - last) / sampleRate;

        (double attackMs, double decayMs, EnvelopeShape shape) = Envelope(context);

        return new DynamicsFacet(
            Decibels.Round(peakDb, 2),
            Decibels.Round(truePeakDb, 2),
            Decibels.Round(rmsDb, 2),
            Decibels.Round(activeRmsDb, 2),
            lufs,
            isGated,
            range,
            Decibels.Round(peakDb - activeRmsDb, 2),
            Decibels.Round(dcOffset, 5),
            CountClippedSamples(channels),
            Decibels.Round(leadMs, 1),
            Decibels.Round(trailMs, 1),
            Decibels.Round((double)active.Length / sampleRate, 3),
            attackMs,
            decayMs,
            shape);
    }
    #endregion
}
