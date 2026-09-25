namespace OvertonesPlayground.Ontology.Analysis.Features;

///<summary>
///Rhythm: onset detection by spectral flux, tempo by autocorrelation of the onset envelope, and whether the clip is a
///clean loop. Only files of at least 1.5 s are analysed; shorter files hold a single hit and return null.
///</summary>
public sealed class RhythmAnalyzer : IFeatureExtractor<RhythmFacet?>
{
    #region Constants
    private const double MinSeconds = 1.5;
    private const double MaxSeconds = 60.0;
    private const double TargetRate = 22050.0;
    private const int FrameSize = 1024;
    private const int Hop = 256;
    private const double MinBpm = 60.0;
    private const double MaxBpm = 200.0;
    private const double PriorBpm = 120.0;
    private const double NameTolerance = 0.04;
    private const double LoopMinConfidence = RhythmFacet.MinTrustedConfidence;
    private const double NormalizedPeak = 0.5;

    private static readonly double[] _combWeights = [1.0, 0.5, 0.33, 0.25];

    // Same tempo, half / double time, and the dotted (3:2, 2:3) and triplet-feel (4:3, 3:4) relations that a drum loop's
    // strongest pulse commonly has to its written beat.
    private static readonly double[] _relatedRatios = [1.0, 2.0, 0.5, 1.5, 2.0 / 3.0, 4.0 / 3.0, 0.75];
    private const double MinOnsetPeak = 20.0;
    private const double LoopBeatTolerance = 0.10;

    // Loops are cut to whole bars, and almost always to a power-of-two number of beats: in this corpus 106 of the 112 files
    // with a tempo in the name are within 1 % of a whole beat count, and 103 of those 106 hold 4, 8, 16 or 32 beats.
    private const double GridMinBpm = 60.0;
    private const double GridMaxBpm = 210.0;
    // The usual range of a written tempo is asymmetric in log terms: between a slow tempo and its double, names lean slow
    // (a "80 bpm" hip-hop loop is more common than a "160 bpm" one), so ties go to the tempo nearer this value.
    private const double GridPriorBpm = 110.0;
    private const double GridTolerance = 0.02;
    private static readonly int[] _gridBeatCounts = [4, 8, 16, 32, 64];
    private static readonly double[] _octaveRatios = [1.0, 2.0, 0.5];
    private static readonly double[] _feelRatios = [1.5, 2.0 / 3.0, 4.0 / 3.0, 0.75];
    #endregion

    #region Private methods
    ///<summary>
    ///Onset strength per frame: the half-wave rectified change of log-compressed magnitude between consecutive frames, with
    ///the local mean removed. Exposed so the scale (real hits reach 100+) can be inspected.
    ///</summary>
    public static double[] OnsetStrength(float[] signal)
    {
        StftEngine engine = new(FrameSize);
        int frames = Math.Max(1, ((signal.Length - FrameSize) / Hop) + 1);
        double[] power = new double[engine.BinCount];
        double[] previous = new double[engine.BinCount];
        double[] strength = new double[frames];
        for (int t = 0; t < frames; t++)
        {
            engine.Power(signal, t * Hop, power);
            double flux = 0;
            for (int b = 1; b < power.Length; b++)
            {
                double level = Math.Log10(1.0 + (1000.0 * Math.Sqrt(power[b]) / FrameSize));
                if (t > 0)
                {
                    flux += Math.Max(0.0, level - previous[b]);
                }

                previous[b] = level;
            }

            strength[t] = flux;
        }

        // Remove the slowly varying local mean so steady loud passages do not read as onsets.
        double[] cleaned = new double[frames];
        int halfWindow = (int)(0.25 * TargetRate / Hop);
        for (int t = 0; t < frames; t++)
        {
            int from = Math.Max(0, t - halfWindow);
            int to = Math.Min(frames - 1, t + halfWindow);
            double sum = 0;
            for (int i = from; i <= to; i++)
            {
                sum += strength[i];
            }

            cleaned[t] = Math.Max(0.0, strength[t] - (sum / (to - from + 1)));
        }

        return cleaned;
    }

    private static int CountOnsets(double[] strength, double framesPerSecond)
    {
        double mean = strength.Average();
        double deviation = Math.Sqrt(strength.Average(value => (value - mean) * (value - mean)));
        // The floor relative to the strongest onset stops numerical noise in a steady tone from counting as onsets.
        double threshold = Math.Max(mean + (0.5 * deviation), 0.25 * strength.Max());
        int minGap = Math.Max(1, (int)(0.05 * framesPerSecond));
        int count = 0;
        int lastOnset = -minGap;
        for (int t = 1; t < strength.Length - 1; t++)
        {
            bool isPeak = strength[t] > threshold && strength[t] >= strength[t - 1] && strength[t] > strength[t + 1];
            if (isPeak && t - lastOnset >= minGap)
            {
                count++;
                lastOnset = t;
            }
        }

        return count;
    }

    ///<summary>Tempo from the strongest periodicity of the onset envelope, weighted toward 120 BPM to prefer musical octaves.</summary>
    private static (double? Bpm, double Confidence) EstimateTempo(double[] strength, double framesPerSecond)
    {
        int n = strength.Length;
        double mean = strength.Average();
        double[] centered = [.. strength.Select(value => value - mean)];
        double energy = centered.Sum(value => value * value);
        bool isFlat = energy <= 1e-12;
        if (isFlat)
        {
            return (null, 0);
        }

        int minLag = Math.Max(1, (int)Math.Floor(framesPerSecond * 60.0 / MaxBpm));
        int maxLag = Math.Min(n / 2, (int)Math.Ceiling(framesPerSecond * 60.0 / MinBpm));
        bool isTooShort = maxLag <= minLag + 2;
        if (isTooShort)
        {
            return (null, 0);
        }

        // Autocorrelation is needed out to a few beat periods, because a tempo is only trusted when its multiples (two
        // beats, three, a bar) line up as well. That is what separates the true beat from a syncopated sub-pulse such as
        // a dotted eighth, which a plain autocorrelation peak locks onto for many drum loops.
        int combTerms = _combWeights.Length;
        int reach = Math.Min(n - 1, (combTerms * maxLag) + 2);
        double[] correlation = new double[reach + 2];
        for (int lag = minLag - 1; lag <= reach + 1 && lag < n; lag++)
        {
            double sum = 0;
            for (int t = 0; t + lag < n; t++)
            {
                sum += centered[t] * centered[t + lag];
            }

            correlation[lag] = sum / (n - lag) / (energy / n);
        }

        int best = -1;
        double bestScore = double.MinValue;
        for (int lag = minLag; lag <= maxLag; lag++)
        {
            bool isPeak = correlation[lag] >= correlation[lag - 1] && correlation[lag] >= correlation[lag + 1];
            if (!isPeak)
            {
                continue;
            }

            double weighted = 0;
            double weights = 0;
            for (int k = 1; k <= combTerms && k * lag <= reach; k++)
            {
                weighted += _combWeights[k - 1] * correlation[k * lag];
                weights += _combWeights[k - 1];
            }

            double bpm = 60.0 * framesPerSecond / lag;
            double octaves = Math.Log2(bpm / PriorBpm);
            double score = weighted / weights * Math.Exp(-0.5 * octaves * octaves);
            if (score > bestScore)
            {
                bestScore = score;
                best = lag;
            }
        }

        if (best < 0)
        {
            return (null, 0);
        }

        double refined = best;
        double s0 = correlation[best - 1];
        double s1 = correlation[best];
        double s2 = correlation[best + 1];
        double denominator = s0 + s2 - (2.0 * s1);
        if (Math.Abs(denominator) > 1e-12)
        {
            refined = best + (0.5 * (s0 - s2) / denominator);
        }

        return (60.0 * framesPerSecond / refined, Math.Clamp(correlation[best], 0.0, 1.0));
    }

    ///<summary>
    ///The tempo, among those a loop of the given length can have, that <paramref name="detected"/> stands for: the grid tempo
    ///(<c>60 * beats / duration</c> for 4, 8, 16, 32 or 64 beats) that the detected tempo or its double or half is within
    ///2 % of, nearest the usual range when there are two.
    ///</summary>
    private static double? NearestGridTempo(double detected, double durationSeconds, double[] ratios)
    {
        double? best = null;
        double bestDistance = double.MaxValue;
        foreach (double ratio in ratios)
        {
            double candidate = detected * ratio;
            if (candidate < GridMinBpm || candidate > GridMaxBpm)
            {
                continue;
            }

            foreach (int beats in _gridBeatCounts)
            {
                double gridTempo = 60.0 * beats / durationSeconds;
                bool isOnGrid = gridTempo is >= GridMinBpm and <= GridMaxBpm && Math.Abs(candidate - gridTempo) / gridTempo <= GridTolerance;
                double distance = Math.Abs(Math.Log2(gridTempo / GridPriorBpm));
                if (isOnGrid && distance < bestDistance)
                {
                    best = gridTempo;
                    bestDistance = distance;
                }
            }
        }

        return best;
    }

    private static bool Agrees(double detected, double named)
    {
        foreach (double factor in _relatedRatios)
        {
            if (Math.Abs((detected * factor) - named) / named <= NameTolerance)
            {
                return true;
            }
        }

        return false;
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Corrects a detected tempo using the length of the clip. The strongest pulse of a drum loop is often not its written beat:
    ///it can be the double or half, or a dotted or triplet relative, and the estimate is only good to a percent or two. But a
    ///loop is cut to whole bars, so its tempo is one of <c>60 * beats / duration</c>. In order:
    ///<list type="number">
    ///<item>if the detected tempo, its double or its half is on that grid, take the grid tempo (the one nearest 110 BPM when
    ///two are);</item>
    ///<item>otherwise, if the detected tempo already gives a whole number of beats, keep it (a 3-bar loop is a loop too);</item>
    ///<item>otherwise, if a dotted or triplet relative (3:2, 2:3, 4:3, 3:4) is on the grid, take that;</item>
    ///<item>otherwise keep the detected tempo.</item>
    ///</list>
    ///Whether a loop is felt at 80 or at 160 is a convention of whoever named it, so the octave choice is a prior, not a
    ///measurement.
    ///</summary>
    ///<param name="detectedBpm">Tempo from the onset envelope.</param>
    ///<param name="durationSeconds">Length of the clip.</param>
    public static double SnapToLoopGrid(double detectedBpm, double durationSeconds)
    {
        if (detectedBpm <= 0 || durationSeconds <= 0)
        {
            return detectedBpm;
        }

        double? sameOrOctave = NearestGridTempo(detectedBpm, durationSeconds, _octaveRatios);
        if (sameOrOctave is { } snapped)
        {
            return snapped;
        }

        double ownBeats = durationSeconds * detectedBpm / 60.0;
        bool isWholeBeats = ownBeats >= 2.0 && Math.Abs(ownBeats - Math.Round(ownBeats)) <= LoopBeatTolerance;
        return isWholeBeats ? detectedBpm : NearestGridTempo(detectedBpm, durationSeconds, _feelRatios) ?? detectedBpm;
    }

    ///<inheritdoc/>
    public RhythmFacet? Extract(AnalysisContext context)
    {
        double duration = context.Audio.DurationSeconds;
        bool isTooShort = duration < MinSeconds || context.IsSilent;
        if (isTooShort)
        {
            return null;
        }

        int sampleRate = context.SampleRate;
        int factor = Decimator.FactorFor(sampleRate, TargetRate);
        int decimatedRate = sampleRate / factor;
        int length = Math.Min(context.Audio.FrameCount, (int)(MaxSeconds * sampleRate));
        float[] signal = Decimator.Decimate(context.Mono.AsSpan(0, length), factor, sampleRate);
        bool isUnusable = signal.Length < FrameSize * 2;
        if (isUnusable)
        {
            return null;
        }

        double framesPerSecond = (double)decimatedRate / Hop;
        double peak = signal.Max(sample => Math.Abs(sample));
        float gain = peak > 0 ? (float)(NormalizedPeak / peak) : 1f;
        double[] strength = OnsetStrength([.. signal.Select(sample => sample * gain)]);

        // Real hits reach 100+ on this scale, atmospheres and noise stay below 10, and a steady tone's rounding
        // ripple is around 0.01, so anything under the floor is not rhythm and must not be given a tempo.
        bool hasRealOnsets = strength.Max() >= MinOnsetPeak;
        int onsets = hasRealOnsets ? CountOnsets(strength, framesPerSecond) : 0;
        double activeSeconds = Math.Max(0.1, (double)context.Active.Length / sampleRate);
        (double? measured, double confidence) = hasRealOnsets ? EstimateTempo(strength, framesPerSecond) : (null, 0.0);

        // A clip that is cut to whole bars can only have a tempo that fits its length; use that to correct octave and
        // triplet-feel mistakes and to make the estimate exact. Only when the periodicity is real, and not for very long files.
        bool canUseLoopLength = measured is not null && confidence >= LoopMinConfidence && duration <= MaxSeconds;
        double? detected = canUseLoopLength ? SnapToLoopGrid(measured!.Value, duration) : measured;

        double? named = context.Named.TempoBpm;
        bool? agrees = detected is null || named is null ? null : Agrees(detected.Value, named.Value);
        double? best = named ?? (confidence >= LoopMinConfidence ? detected : null);
        double? beats = best is null ? null : duration * best.Value / 60.0;
        double? bars = beats is null ? null : beats.Value / 4.0;

        // Whether the clip is a loop is judged on the tempo as measured (or as named), not on the loop-length correction above:
        // that correction always lands on a whole number of beats, so it cannot be allowed to vouch for the loop.
        double? measuredBest = named ?? (confidence >= LoopMinConfidence ? measured : null);
        double? measuredBeats = measuredBest is null ? null : duration * measuredBest.Value / 60.0;
        bool isWholeBeats = measuredBeats is { } b && b >= 2.0 && Math.Abs(b - Math.Round(b)) <= LoopBeatTolerance;
        bool isPeriodic = named is not null || confidence >= LoopMinConfidence;

        return new RhythmFacet(
            detected is null ? null : Decibels.Round(detected.Value, 1),
            Decibels.Round(confidence, 3),
            Decibels.Round(onsets / activeSeconds, 3),
            onsets,
            agrees,
            beats is null ? null : Decibels.Round(beats.Value, 2),
            bars is null ? null : Decibels.Round(bars.Value, 2),
            isWholeBeats && isPeriodic);
    }
    #endregion
}
