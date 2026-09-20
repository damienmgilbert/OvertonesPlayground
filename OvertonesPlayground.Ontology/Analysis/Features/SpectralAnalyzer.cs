namespace OvertonesPlayground.Ontology.Analysis.Features;

///<summary>
///Spectral content: centroid, rolloff, bandwidth, flatness, flux, tilt and energy per Hz-defined band. Frames are
///Hann-windowed 4096-point FFTs over the audible part of the file; their power spectra are summed, which weights each
///frame by its energy so a quiet tail cannot dilute the character of a short hit.
///</summary>
public sealed class SpectralAnalyzer : IFeatureExtractor<SpectralFacet>
{
    #region Constants
    private const int FftSize = 4096;
    private const double MaxAnalysisSeconds = 30.0;
    private const double RolloffFraction = 0.85;
    private const double TiltFirstCenterHz = 50.0;
    #endregion

    #region Private methods
    private const int MelFilters = 26;
    private const int MfccCount = 12;
    private const double OnsetWindowSeconds = 0.023;

    private static SpectralFacet Empty() => new(0, 0, 0, 0, 0, 0, new BandEnergies(0, 0, 0, 0, 0, 0, 0), 0, new double[MfccCount]);

    private static double HzToMel(double hz) => 2595.0 * Math.Log10(1.0 + (hz / 700.0));

    private static double MelToHz(double mel) => 700.0 * (Math.Pow(10.0, mel / 2595.0) - 1.0);

    private static int NearestPowerOfTwo(double value) => 1 << (int)Math.Round(Math.Log2(Math.Max(2.0, value)));

    ///<summary>
    ///Mel-frequency cepstral coefficients 1 - 12 of a power spectrum. Coefficient 0 (overall level) is left out, so the
    ///result describes spectral shape only and does not change with gain or with the number of frames summed.
    ///</summary>
    private static double[] Mfcc(double[] power, double binHz, double nyquistHz)
    {
        double lowMel = HzToMel(30.0);
        double highMel = HzToMel(Math.Min(16000.0, 0.95 * nyquistHz));
        double[] edges = new double[MelFilters + 2];
        for (int i = 0; i < edges.Length; i++)
        {
            edges[i] = MelToHz(lowMel + ((highMel - lowMel) * i / (MelFilters + 1)));
        }

        double[] energy = new double[MelFilters];
        for (int m = 0; m < MelFilters; m++)
        {
            double low = edges[m];
            double middle = edges[m + 1];
            double high = edges[m + 2];
            for (int b = Math.Max(1, (int)(low / binHz)); b < power.Length && b * binHz < high; b++)
            {
                double frequency = b * binHz;
                double weight = frequency <= low ? 0.0 : frequency <= middle ? (frequency - low) / (middle - low) : (high - frequency) / (high - middle);
                energy[m] += weight * power[b];
            }
        }

        double floor = (energy.Max() * 1e-10) + 1e-30;
        double[] logEnergy = [.. energy.Select(value => Math.Log(value + floor))];
        double[] coefficients = new double[MfccCount];
        double scale = Math.Sqrt(2.0 / MelFilters);
        for (int k = 1; k <= MfccCount; k++)
        {
            double sum = 0;
            for (int m = 0; m < MelFilters; m++)
            {
                sum += logEnergy[m] * Math.Cos(Math.PI * k * (m + 0.5) / MelFilters);
            }

            coefficients[k - 1] = Decibels.Round(sum * scale, 3);
        }

        return coefficients;
    }

    ///<summary>Centroid of one window placed so the attack sits a quarter of the way in.</summary>
    private static double OnsetCentroid(ReadOnlySpan<float> signal, int start, int sampleRate)
    {
        int size = Math.Clamp(NearestPowerOfTwo(OnsetWindowSeconds * sampleRate), 512, 4096);
        StftEngine engine = new(size);
        double[] power = new double[engine.BinCount];
        engine.Power(signal, start - (size / 4), power);
        double binHz = (double)sampleRate / size;
        double total = 0;
        double weighted = 0;
        for (int b = 1; b < power.Length; b++)
        {
            total += power[b];
            weighted += b * binHz * power[b];
        }

        return total <= 1e-20 ? 0 : weighted / total;
    }

    ///<summary>Slope of the spectrum in dB per octave, fitted over third-octave band levels.</summary>
    private static double Tilt(double[] power, double binHz, double nyquistHz)
    {
        List<(double LogFrequency, double Level)> points = [];
        double ceiling = 0.9 * nyquistHz;
        for (double center = TiltFirstCenterHz; center < ceiling; center *= Math.Pow(2.0, 1.0 / 3.0))
        {
            double low = center / Math.Pow(2.0, 1.0 / 6.0);
            double high = Math.Min(center * Math.Pow(2.0, 1.0 / 6.0), nyquistHz);
            int from = Math.Max(1, (int)Math.Ceiling(low / binHz));
            int to = Math.Min(power.Length - 1, (int)Math.Floor(high / binHz));
            if (to < from)
            {
                continue;
            }

            double sum = 0;
            for (int b = from; b <= to; b++)
            {
                sum += power[b];
            }

            points.Add((Math.Log2(center), Decibels.FromPower((sum / (to - from + 1)) + 1e-20)));
        }

        if (points.Count < 3)
        {
            return 0;
        }

        double meanX = points.Average(p => p.LogFrequency);
        double meanY = points.Average(p => p.Level);
        double covariance = 0;
        double variance = 0;
        foreach ((double x, double y) in points)
        {
            covariance += (x - meanX) * (y - meanY);
            variance += (x - meanX) * (x - meanX);
        }

        return variance <= 0 ? 0 : covariance / variance;
    }
    #endregion

    #region Public methods
    ///<inheritdoc/>
    public SpectralFacet Extract(AnalysisContext context)
    {
        if (context.IsSilent)
        {
            return Empty();
        }

        int sampleRate = context.SampleRate;
        double nyquist = sampleRate / 2.0;
        StftEngine engine = new(FftSize);
        double binHz = (double)sampleRate / FftSize;
        int bins = engine.BinCount;

        ActiveRegion active = context.Active;
        int end = Math.Min(active.EndFrame, active.StartFrame + (int)(MaxAnalysisSeconds * sampleRate));
        ReadOnlySpan<float> signal = context.Mono.AsSpan(0, end);
        int hop = FftSize / 2;
        int position = active.StartFrame - (FftSize / 4);

        double[] total = new double[bins];
        double[] frame = new double[bins];
        double[] previousMagnitude = new double[bins];
        double fluxSum = 0;
        int fluxFrames = 0;
        bool isFirst = true;

        do
        {
            engine.Power(signal, position, frame);
            double magnitudeSum = 0;
            double positiveChange = 0;
            for (int b = 1; b < bins; b++)
            {
                total[b] += frame[b];
                double magnitude = Math.Sqrt(frame[b]);
                magnitudeSum += magnitude;
                if (!isFirst)
                {
                    positiveChange += Math.Max(0.0, magnitude - previousMagnitude[b]);
                }

                previousMagnitude[b] = magnitude;
            }

            if (!isFirst && magnitudeSum > 1e-9)
            {
                fluxSum += positiveChange / magnitudeSum;
                fluxFrames++;
            }

            isFirst = false;
            position += hop;
        }
        while (position + (FftSize / 2) < end);

        double totalPower = 0;
        double weightedFrequency = 0;
        for (int b = 1; b < bins; b++)
        {
            totalPower += total[b];
            weightedFrequency += b * binHz * total[b];
        }

        if (totalPower <= 1e-20)
        {
            return Empty();
        }

        // Power weighting keeps window leakage and the low-level noise floor of long tails from dragging the
        // centroid upward, which magnitude weighting would do.
        double centroid = weightedFrequency / totalPower;
        double varianceSum = 0;
        for (int b = 1; b < bins; b++)
        {
            double delta = (b * binHz) - centroid;
            varianceSum += delta * delta * total[b];
        }

        double bandwidth = Math.Sqrt(varianceSum / totalPower);

        double cumulative = 0;
        double rolloff = nyquist;
        for (int b = 1; b < bins; b++)
        {
            cumulative += total[b];
            if (cumulative >= RolloffFraction * totalPower)
            {
                rolloff = b * binHz;
                break;
            }
        }

        int flatFrom = Math.Max(1, (int)Math.Ceiling(20.0 / binHz));
        int flatTo = Math.Min(bins - 1, (int)Math.Floor(Math.Min(20000.0, 0.95 * nyquist) / binHz));
        double logSum = 0;
        double linearSum = 0;
        int flatCount = 0;
        for (int b = flatFrom; b <= flatTo; b++)
        {
            double magnitude = Math.Sqrt(total[b]);
            logSum += Math.Log(magnitude + 1e-12);
            linearSum += magnitude;
            flatCount++;
        }

        double flatness = flatCount == 0 || linearSum <= 0 ? 0 : Math.Exp(logSum / flatCount) / (linearSum / flatCount);

        double[] bands = new double[FrequencyBands.Count];
        for (int b = 1; b < bins; b++)
        {
            bands[(int)FrequencyBands.FromFrequency(b * binHz)] += total[b];
        }

        for (int i = 0; i < bands.Length; i++)
        {
            bands[i] = Decibels.Round(bands[i] / totalPower, 4);
        }

        return new SpectralFacet(
            Decibels.Round(centroid, 1),
            Decibels.Round(rolloff, 1),
            Decibels.Round(bandwidth, 1),
            Decibels.Round(Math.Clamp(flatness, 0, 1), 4),
            Decibels.Round(fluxFrames == 0 ? 0 : fluxSum / fluxFrames, 4),
            Decibels.Round(Tilt(total, binHz, nyquist), 2),
            BandEnergies.FromArray(bands),
            Decibels.Round(OnsetCentroid(context.Mono, active.StartFrame, sampleRate), 1),
            Mfcc(total, binHz, nyquist));
    }
    #endregion
}
