namespace OvertonesPlayground.Ontology.Analysis.Dsp;

///<summary>
///Amplitude envelopes and the audible region of a signal.
///</summary>
public static class EnvelopeTools
{
    ///<summary>
    ///The stretch of the signal whose RMS envelope stays within <paramref name="thresholdDb"/> of its own peak. Empty
    ///for silence.
    ///</summary>
    public static ActiveRegion FindActiveRegion(double[] rmsEnvelope, int hopSamples, int windowSamples, int frames, double thresholdDb = -50.0)
    {
        double peak = 0;
        foreach (double value in rmsEnvelope)
        {
            peak = Math.Max(peak, value);
        }

        bool isSilent = peak <= 1e-9;
        if (isSilent)
        {
            return new ActiveRegion(0, 0);
        }

        double threshold = peak * Decibels.ToAmplitude(thresholdDb);
        int first = 0;
        while (first < rmsEnvelope.Length && rmsEnvelope[first] < threshold)
        {
            first++;
        }

        int last = rmsEnvelope.Length - 1;
        while (last > first && rmsEnvelope[last] < threshold)
        {
            last--;
        }

        int start = Math.Max(0, (first * hopSamples) - (windowSamples / 2));
        int end = Math.Min(frames, (last * hopSamples) + (windowSamples / 2));
        return new ActiveRegion(start, Math.Max(start + 1, end));
    }

    ///<summary>
    ///Peak-hold envelope over all channels: the largest absolute sample in a trailing window, sampled every hop. Rises
    ///exactly when the signal rises, then falls smoothly, so attack times stay accurate for low-frequency material.
    ///</summary>
    public static double[] PeakHoldEnvelope(float[][] channels, int holdSamples, int hopSamples)
    {
        int n = channels.Length == 0 ? 0 : channels[0].Length;
        int hops = n == 0 ? 0 : ((n - 1) / hopSamples) + 1;
        double[] blocks = new double[hops];
        for (int k = 0; k < hops; k++)
        {
            int from = k * hopSamples;
            int to = Math.Min(n, from + hopSamples);
            double max = 0;
            foreach (float[] channel in channels)
            {
                for (int i = from; i < to; i++)
                {
                    double magnitude = Math.Abs(channel[i]);
                    if (magnitude > max)
                    {
                        max = magnitude;
                    }
                }
            }

            blocks[k] = max;
        }

        int window = Math.Max(1, holdSamples / hopSamples);
        double[] envelope = new double[hops];
        for (int k = 0; k < hops; k++)
        {
            double max = 0;
            for (int j = Math.Max(0, k - window + 1); j <= k; j++)
            {
                if (blocks[j] > max)
                {
                    max = blocks[j];
                }
            }

            envelope[k] = max;
        }

        return envelope;
    }

    ///<summary>
    ///RMS envelope: for each hop, the RMS of a window centred on it, computed from prefix sums of squares in O(n).
    ///</summary>
    public static double[] RmsEnvelope(ReadOnlySpan<float> mono, int windowSamples, int hopSamples)
    {
        int n = mono.Length;
        double[] prefix = new double[n + 1];
        for (int i = 0; i < n; i++)
        {
            prefix[i + 1] = prefix[i] + ((double)mono[i] * mono[i]);
        }

        int hops = n == 0 ? 0 : ((n - 1) / hopSamples) + 1;
        double[] envelope = new double[hops];
        for (int k = 0; k < hops; k++)
        {
            int center = k * hopSamples;
            int from = Math.Max(0, center - (windowSamples / 2));
            int to = Math.Min(n, center + (windowSamples / 2));
            int count = to - from;
            envelope[k] = count <= 0 ? 0.0 : Math.Sqrt(Math.Max(0.0, prefix[to] - prefix[from]) / count);
        }

        return envelope;
    }
}
