namespace OvertonesPlayground.Services.Implementations;

///<summary>
///Classic spectral-subtraction noise reduction: estimate a steady-state noise profile from a quiet stretch at the
///start of the clip, then subtract it from every analysis frame's magnitude spectrum across the whole clip, keeping
///the original phase. Runs independently per channel using <see cref="FastFourierTransform"/>.
///</summary>
internal static class SpectralNoiseReducer
{
    #region Constants
    ///<summary>
    ///FFT window size. Large enough for reasonable frequency resolution on speech/music-range noise, small enough
    ///that a single frame is a fraction of a second even at typical sample rates.
    ///</summary>
    private const int FftSize = 2048;

    ///<summary>
    ///Frames advance by half the FFT size (50% overlap), the standard hop for Hann-windowed overlap-add.
    ///</summary>
    private const int HopSize = FftSize / 2;

    ///<summary>
    ///Floor on the subtracted magnitude, as a fraction of the frame's own magnitude - prevents bins from being
    ///subtracted to zero (or negative), which produces audible "musical noise" artifacts.
    ///</summary>
    private const double NoiseFloorRatio = 0.02;

    ///<summary>
    ///How much of the estimated noise magnitude to subtract per bin. Above 1.0 to bias toward removing noise more
    ///aggressively than a literal average, since the estimate is itself noisy.
    ///</summary>
    private const double OverSubtractionFactor = 2.0;
    #endregion

    #region Private methods
    ///<summary>
    ///Averages the magnitude spectrum of every full analysis frame within the first <paramref name="noiseFrameCount"/>
    ///samples, to use as the noise profile subtracted from the whole clip.
    ///</summary>
    private static double[] BuildNoiseProfile(double[] samples, int noiseFrameCount, double[] window)
    {
        double[] magnitudeSum = new double[FftSize];
        int windowCount = 0;

        for (int start = 0; start + FftSize <= noiseFrameCount; start += HopSize)
        {
            double[] real = new double[FftSize];
            double[] imaginary = new double[FftSize];
            for (int i = 0; i < FftSize; i++)
            {
                real[i] = samples[start + i] * window[i];
            }

            FastFourierTransform.Forward(real, imaginary);
            for (int i = 0; i < FftSize; i++)
            {
                magnitudeSum[i] += Math.Sqrt((real[i] * real[i]) + (imaginary[i] * imaginary[i]));
            }

            windowCount++;
        }

        bool hasNoFullWindow = windowCount == 0;
        if (hasNoFullWindow)
        {
            double[] real = new double[FftSize];
            double[] imaginary = new double[FftSize];
            int available = Math.Min(noiseFrameCount, FftSize);
            for (int i = 0; i < available; i++)
            {
                real[i] = samples[i] * window[i];
            }

            FastFourierTransform.Forward(real, imaginary);
            for (int i = 0; i < FftSize; i++)
            {
                magnitudeSum[i] = Math.Sqrt((real[i] * real[i]) + (imaginary[i] * imaginary[i]));
            }

            windowCount = 1;
        }

        for (int i = 0; i < FftSize; i++)
        {
            magnitudeSum[i] /= windowCount;
        }

        return magnitudeSum;
    }

    ///<summary>
    ///Builds a Hann window of the given size, used both at analysis (before the forward FFT) and synthesis (after
    ///the inverse FFT), which is standard practice for magnitude-modification overlap-add.
    ///</summary>
    private static double[] BuildHannWindow(int size)
    {
        double[] window = new double[size];
        for (int i = 0; i < size; i++)
        {
            window[i] = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (size - 1)));
        }

        return window;
    }

    ///<summary>
    ///Runs spectral subtraction over one channel's samples (already normalized to [-1, 1]) and returns the result in
    ///the same range.
    ///</summary>
    private static double[] ReduceChannel(double[] samples, int noiseFrameCount)
    {
        double[] window = BuildHannWindow(FftSize);
        double[] noiseProfile = BuildNoiseProfile(samples, noiseFrameCount, window);

        double[] output = new double[samples.Length];
        double[] windowSum = new double[samples.Length];

        for (int start = 0; start + FftSize <= samples.Length; start += HopSize)
        {
            double[] real = new double[FftSize];
            double[] imaginary = new double[FftSize];
            for (int i = 0; i < FftSize; i++)
            {
                real[i] = samples[start + i] * window[i];
            }

            FastFourierTransform.Forward(real, imaginary);

            for (int i = 0; i < FftSize; i++)
            {
                double magnitude = Math.Sqrt((real[i] * real[i]) + (imaginary[i] * imaginary[i]));
                bool hasMagnitude = magnitude > 0;
                double phaseReal = hasMagnitude ? real[i] / magnitude : 0;
                double phaseImag = hasMagnitude ? imaginary[i] / magnitude : 0;

                double subtracted = magnitude - (OverSubtractionFactor * noiseProfile[i]);
                double floor = NoiseFloorRatio * magnitude;
                double newMagnitude = Math.Max(subtracted, floor);

                real[i] = newMagnitude * phaseReal;
                imaginary[i] = newMagnitude * phaseImag;
            }

            FastFourierTransform.Inverse(real, imaginary);

            for (int i = 0; i < FftSize; i++)
            {
                output[start + i] += real[i] * window[i];
                windowSum[start + i] += window[i] * window[i];
            }
        }

        for (int i = 0; i < output.Length; i++)
        {
            bool hasWeight = windowSum[i] > 1e-6;
            if (hasWeight)
            {
                output[i] /= windowSum[i];
            }
        }

        return output;
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Reduces steady-state noise (hum, hiss, fan/AC rumble) in <paramref name="samples"/>, estimating the noise
    ///profile from each channel's own first <paramref name="noiseFrameCount"/> frames.
    ///</summary>
    public static short[] Reduce(short[] samples, int channels, int noiseFrameCount)
    {
        bool isEmpty = samples.Length == 0 || channels <= 0;
        if (isEmpty)
        {
            return samples;
        }

        int frames = samples.Length / channels;
        int clampedNoiseFrameCount = Math.Clamp(noiseFrameCount, 0, frames);

        double[][] channelOutputs = new double[channels][];
        for (int channel = 0; channel < channels; channel++)
        {
            double[] channelSamples = new double[frames];
            for (int frame = 0; frame < frames; frame++)
            {
                channelSamples[frame] = samples[(frame * channels) + channel] / (double)short.MaxValue;
            }

            channelOutputs[channel] = ReduceChannel(channelSamples, clampedNoiseFrameCount);
        }

        short[] output = new short[samples.Length];
        for (int frame = 0; frame < frames; frame++)
        {
            for (int channel = 0; channel < channels; channel++)
            {
                output[(frame * channels) + channel] = PcmMath.ClampToShort(channelOutputs[channel][frame] * short.MaxValue);
            }
        }

        return output;
    }
    #endregion
}
