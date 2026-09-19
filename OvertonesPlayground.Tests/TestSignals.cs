namespace OvertonesPlayground.Tests;

/// <summary>
/// Builds and measures 16-bit PCM test signals, so each test states what it feeds in and reads out rather than
/// re-deriving sample math.
/// </summary>
internal static class TestSignals
{
    /// <summary>
    /// Generates an interleaved sine wave; every channel carries the same signal.
    /// </summary>
    public static short[] Sine(double frequencyHz, int sampleRate, double seconds, double amplitude = 0.5, int channels = 1)
    {
        int frames = (int)(seconds * sampleRate);
        short[] samples = new short[frames * channels];
        for (int frame = 0; frame < frames; frame++)
        {
            short value = (short)(Math.Sin(2 * Math.PI * frequencyHz * frame / sampleRate) * amplitude * short.MaxValue);
            for (int channel = 0; channel < channels; channel++)
            {
                samples[(frame * channels) + channel] = value;
            }
        }

        return samples;
    }

    /// <summary>
    /// Root-mean-square level of <paramref name="samples"/> from <paramref name="startIndex"/> on, normalized to 0..1.
    /// Skipping the start lets a test ignore a filter's start-up transient.
    /// </summary>
    public static double Rms(short[] samples, int startIndex = 0)
    {
        double sumOfSquares = 0;
        for (int i = startIndex; i < samples.Length; i++)
        {
            double normalized = samples[i] / (double)short.MaxValue;
            sumOfSquares += normalized * normalized;
        }

        return Math.Sqrt(sumOfSquares / (samples.Length - startIndex));
    }
}
