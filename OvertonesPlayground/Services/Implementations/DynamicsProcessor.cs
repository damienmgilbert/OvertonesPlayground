namespace OvertonesPlayground.Services.Implementations;

///<summary>
///A feed-forward peak compressor: an attack/release-smoothed envelope follower feeds a static threshold/ratio gain
///curve, applied uniformly across every channel each frame so a stereo signal's image doesn't shift.
///</summary>
internal static class DynamicsProcessor
{
    #region Constants
    ///<summary>
    ///Floor for attack/release times, in milliseconds - guards against a zero or negative time collapsing the
    ///smoothing coefficient's exponent.
    ///</summary>
    private const double MinTimeConstantMs = 1;
    #endregion

    #region Public methods
    ///<summary>
    ///Compresses <paramref name="samples"/> in place: while the smoothed envelope exceeds <paramref name="thresholdDb"/>,
    ///gain is reduced so the excess above threshold is scaled down by <paramref name="ratio"/>.
    ///</summary>
    public static void Compress(short[] samples, int channels, int sampleRate, double thresholdDb, double ratio, double attackMs, double releaseMs)
    {
        bool isEmpty = samples.Length == 0 || channels <= 0;
        if (isEmpty)
        {
            return;
        }

        double thresholdLinear = Math.Pow(10, thresholdDb / 20.0);
        double attackCoefficient = Math.Exp(-1.0 / (Math.Max(MinTimeConstantMs, attackMs) / 1000.0 * sampleRate));
        double releaseCoefficient = Math.Exp(-1.0 / (Math.Max(MinTimeConstantMs, releaseMs) / 1000.0 * sampleRate));

        double envelope = 0;
        int frames = samples.Length / channels;
        for (int frame = 0; frame < frames; frame++)
        {
            int baseIndex = frame * channels;

            double peak = 0;
            for (int channel = 0; channel < channels; channel++)
            {
                double magnitude = Math.Abs(samples[baseIndex + channel] / (double)short.MaxValue);
                if (magnitude > peak)
                {
                    peak = magnitude;
                }
            }

            double coefficient = peak > envelope ? attackCoefficient : releaseCoefficient;
            envelope = (coefficient * envelope) + ((1 - coefficient) * peak);

            double gain = 1.0;
            bool isAboveThreshold = envelope > thresholdLinear && envelope > 0;
            if (isAboveThreshold)
            {
                double envelopeDb = 20 * Math.Log10(envelope);
                double compressedDb = thresholdDb + ((envelopeDb - thresholdDb) / ratio);
                gain = Math.Pow(10, (compressedDb - envelopeDb) / 20.0);
            }

            for (int channel = 0; channel < channels; channel++)
            {
                int index = baseIndex + channel;
                samples[index] = PcmMath.ClampToShort(samples[index] * gain);
            }
        }
    }
    #endregion
}
