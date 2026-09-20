namespace OvertonesPlayground.Ontology.Analysis.Features;

///<summary>
///Sound character: is the sample pitched, at what fundamental, how harmonic and how bright. Pitch is measured with YIN
///on a few windows placed after the attack (where a drum hit has settled into its tone) and the most periodic one wins.
///Descriptor thresholds are collected here so they can be tuned in one place.
///</summary>
public sealed class TonalityAnalyzer : IFeatureExtractor<TonalityFacet>
{
    #region Constants
    private const double PitchedConfidence = 0.85;
    private const double ReportedConfidence = 0.70;
    private const int WindowSize = 4096;
    private const int MinWindowSize = 1024;
    private const double TargetRate = 22050.0;

    private const double NoisyFlatness = 0.25;
    private const double DarkCentroidHz = 700.0;
    private const double WarmCentroidHz = 1800.0;
    private const double BrightCentroidHz = 3500.0;
    private const double SubEnergyShare = 0.40;
    private const double HarmonicMaxInharmonicity = 0.02;
    private const double InharmonicMinInharmonicity = 0.03;
    private const double MetallicHighShare = 0.60;
    private const double MetallicMinDecayMs = 250.0;

    private static readonly double[] _windowOffsetsSeconds = [0.005, 0.02, 0.06, 0.15, 0.35];
    #endregion

    #region Private methods
    private static double Rms(ReadOnlySpan<float> window)
    {
        double sum = 0;
        foreach (float sample in window)
        {
            sum += (double)sample * sample;
        }

        return window.Length == 0 ? 0 : Math.Sqrt(sum / window.Length);
    }

    private static int LargestPowerOfTwoAtMost(int value)
    {
        int power = 1;
        while ((power << 1) <= value)
        {
            power <<= 1;
        }

        return power;
    }

    ///<summary>
    ///Mean relative deviation of partials 2 - 8 from integer multiples of the fundamental; 0 when fewer than two are found.
    ///</summary>
    private static double Inharmonicity(ReadOnlySpan<float> window, int sampleRate, double fundamentalHz)
    {
        int size = window.Length;
        StftEngine engine = new(size);
        double[] power = new double[engine.BinCount];
        engine.Power(window, 0, power);
        double binHz = (double)sampleRate / size;

        double fundamentalPeak = 0;
        int fundamentalFrom = Math.Max(1, (int)(fundamentalHz * 0.97 / binHz));
        int fundamentalTo = Math.Min(power.Length - 2, (int)(fundamentalHz * 1.03 / binHz) + 1);
        for (int b = fundamentalFrom; b <= fundamentalTo; b++)
        {
            fundamentalPeak = Math.Max(fundamentalPeak, power[b]);
        }

        double floor = fundamentalPeak * 1e-4;
        double deviationSum = 0;
        int found = 0;
        for (int k = 2; k <= 8; k++)
        {
            double target = k * fundamentalHz;
            bool isAboveLimit = target > 0.45 * sampleRate;
            if (isAboveLimit)
            {
                break;
            }

            int from = Math.Max(1, (int)(target * 0.96 / binHz));
            int to = Math.Min(power.Length - 2, (int)(target * 1.04 / binHz) + 1);
            int peak = from;
            for (int b = from; b <= to; b++)
            {
                if (power[b] > power[peak])
                {
                    peak = b;
                }
            }

            bool isLocalMaximum = peak > 0 && peak < power.Length - 1 && power[peak] >= power[peak - 1] && power[peak] >= power[peak + 1];
            bool isStrongEnough = power[peak] > floor && fundamentalPeak > 0;
            if (!isLocalMaximum || !isStrongEnough)
            {
                continue;
            }

            double s0 = Math.Log(power[peak - 1] + 1e-30);
            double s1 = Math.Log(power[peak] + 1e-30);
            double s2 = Math.Log(power[peak + 1] + 1e-30);
            double denominator = s0 + s2 - (2.0 * s1);
            double offset = Math.Abs(denominator) < 1e-12 ? 0 : 0.5 * (s0 - s2) / denominator;
            double frequency = (peak + offset) * binHz;
            deviationSum += Math.Abs(frequency - target) / target;
            found++;
        }

        return found < 2 ? 0 : deviationSum / found;
    }
    #endregion

    #region Public methods
    ///<inheritdoc/>
    public TonalityFacet Extract(AnalysisContext context)
    {
        if (context.IsSilent)
        {
            return new TonalityFacet(null, null, 0, -10, 0, TonalCharacter.Unpitched);
        }

        int sampleRate = context.SampleRate;
        int frames = context.Audio.FrameCount;
        int factor = Decimator.FactorFor(sampleRate, TargetRate);
        int decimatedRate = sampleRate / factor;

        double[] peakEnvelope = context.PeakEnvelope;
        int peakIndex = 0;
        for (int i = 1; i < peakEnvelope.Length; i++)
        {
            if (peakEnvelope[i] > peakEnvelope[peakIndex])
            {
                peakIndex = i;
            }
        }

        int peakSample = Math.Min(frames - 1, (peakIndex + 1) * context.PeakHopSamples);
        int regionStart = Math.Max(0, peakSample - (sampleRate / 10));
        int regionEnd = Math.Min(frames, regionStart + (4 * sampleRate));
        float[] region = Decimator.Decimate(context.Mono.AsSpan(regionStart, regionEnd - regionStart), factor, sampleRate);
        int peakDecimated = (peakSample - regionStart) / factor;
        double referenceRms = context.RmsEnvelope.Length == 0 ? 0 : context.RmsEnvelope.Max();

        PitchEstimate? best = null;
        int bestStart = 0;
        int bestSize = 0;
        foreach (double offset in _windowOffsetsSeconds)
        {
            int start = peakDecimated + (int)(offset * decimatedRate);
            int available = region.Length - start;
            int size = available >= WindowSize ? WindowSize : LargestPowerOfTwoAtMost(Math.Max(1, available));
            bool isUsable = start >= 0 && size >= MinWindowSize;
            if (!isUsable)
            {
                continue;
            }

            ReadOnlySpan<float> window = region.AsSpan(start, size);
            bool isTooQuiet = Rms(window) < referenceRms * 0.01;
            if (isTooQuiet)
            {
                continue;
            }

            PitchEstimate? estimate = YinPitchDetector.Estimate(window, decimatedRate);
            if (estimate is not null && (best is null || estimate.Value.Confidence > best.Value.Confidence + 1e-9))
            {
                best = estimate;
                bestStart = start;
                bestSize = size;
            }
        }

        double confidence = best?.Confidence ?? 0;
        bool isPitched = confidence >= PitchedConfidence;
        bool isReported = confidence >= ReportedConfidence && best is not null;
        double? fundamental = isReported ? best!.Value.FrequencyHz : null;
        double? midi = fundamental is null ? null : MusicalNotes.MidiOf(fundamental.Value);
        double inharmonicity = isPitched && bestSize >= MinWindowSize
            ? Inharmonicity(region.AsSpan(bestStart, bestSize), decimatedRate, best!.Value.FrequencyHz)
            : 0;

        TonalCharacter character = TonalCharacter.None;
        if (isPitched)
        {
            character |= TonalCharacter.Pitched;
            if (inharmonicity < HarmonicMaxInharmonicity)
            {
                character |= TonalCharacter.Harmonic;
            }
            else if (inharmonicity >= InharmonicMinInharmonicity)
            {
                character |= TonalCharacter.Inharmonic;
            }
        }
        else
        {
            character |= TonalCharacter.Unpitched;
        }

        SpectralFacet? spectral = context.Spectral;
        if (spectral is not null)
        {
            if (spectral.Flatness >= NoisyFlatness)
            {
                character |= TonalCharacter.Noisy;
            }

            if (spectral.CentroidHz < DarkCentroidHz)
            {
                character |= TonalCharacter.Dark;
            }
            else if (spectral.CentroidHz < WarmCentroidHz)
            {
                character |= TonalCharacter.Warm;
            }
            else if (spectral.CentroidHz >= BrightCentroidHz)
            {
                character |= TonalCharacter.Bright;
            }

            if (spectral.Bands.Sub >= SubEnergyShare)
            {
                character |= TonalCharacter.Sub;
            }

            bool isRingingAndBright = spectral.CentroidHz >= BrightCentroidHz
                && spectral.Bands.Presence + spectral.Bands.Air >= MetallicHighShare
                && (context.Dynamics?.DecayMs ?? 0) >= MetallicMinDecayMs;
            if (isRingingAndBright || character.HasFlag(TonalCharacter.Inharmonic))
            {
                character |= TonalCharacter.Metallic;
            }
        }

        return new TonalityFacet(
            fundamental is null ? null : Decibels.Round(fundamental.Value, 2),
            midi is null ? null : Decibels.Round(midi.Value, 2),
            Decibels.Round(confidence, 3),
            Decibels.Round(best?.HarmonicToNoiseDb ?? -10, 2),
            Decibels.Round(inharmonicity, 4),
            character);
    }
    #endregion
}
