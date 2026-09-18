namespace OvertonesPlayground.Services.Implementations;

///<summary>
///Makes two WAV files combinable when they don't already share a channel count and sample rate - the prerequisite
///for every operation that mixes, merges, or inserts one clip into another.
///</summary>
internal static class AudioFormatUtility
{
    #region Private methods
    ///<summary>
    ///Converts interleaved PCM samples between mono and stereo: mono to stereo duplicates each sample across both
    ///channels; stereo to mono averages the pair.
    ///</summary>
    ///<exception cref="NotSupportedException">
    ///The requested conversion isn't mono-to-stereo or stereo-to-mono.
    ///</exception>
    private static short[] ConvertChannels(short[] samples, int sourceChannels, int targetChannels)
    {
        if (sourceChannels == 1 && targetChannels == 2)
        {
            short[] output = new short[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                output[i * 2] = samples[i];
                output[(i * 2) + 1] = samples[i];
            }

            return output;
        }

        if (sourceChannels == 2 && targetChannels == 1)
        {
            int frames = samples.Length / 2;
            short[] output = new short[frames];
            for (int i = 0; i < frames; i++)
            {
                output[i] = (short)((samples[i * 2] + samples[(i * 2) + 1]) / 2);
            }

            return output;
        }

        throw new NotSupportedException($"Cannot convert {sourceChannels}-channel audio to {targetChannels} channel(s); only mono/stereo conversion is supported.");
    }

    ///<summary>
    ///Resamples interleaved PCM samples from <paramref name="sourceRate"/> to <paramref name="targetRate"/> by
    ///linearly interpolating between the nearest source samples, independently for each channel.
    ///</summary>
    private static short[] Resample(short[] samples, int channels, int sourceRate, int targetRate)
    {
        int sourceFrames = samples.Length / channels;
        int targetFrames = (int)Math.Round((double)sourceFrames * targetRate / sourceRate);
        short[] output = new short[targetFrames * channels];

        double step = (double)sourceRate / targetRate;
        for (int frame = 0; frame < targetFrames; frame++)
        {
            double sourcePosition = frame * step;
            int index0 = (int)sourcePosition;
            int index1 = Math.Min(index0 + 1, sourceFrames - 1);
            double fraction = sourcePosition - index0;

            for (int channel = 0; channel < channels; channel++)
            {
                short sample0 = samples[(index0 * channels) + channel];
                short sample1 = samples[(index1 * channels) + channel];
                output[(frame * channels) + channel] = PcmMath.ClampToShort(sample0 + ((sample1 - sample0) * fraction));
            }
        }

        return output;
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Returns <paramref name="source"/>'s samples converted to <paramref name="targetChannels"/> channels and
    ///<paramref name="targetSampleRate"/>, or the samples unchanged if it already matches.
    ///</summary>
    ///<exception cref="NotSupportedException">
    ///<paramref name="source"/>'s channel count can't be converted to <paramref name="targetChannels"/> (only
    ///mono/stereo conversion is supported).
    ///</exception>
    public static short[] Conform(WavFile source, int targetChannels, int targetSampleRate)
    {
        short[] samples = source.Samples;

        if (source.Channels != targetChannels)
        {
            samples = ConvertChannels(samples, source.Channels, targetChannels);
        }

        if (source.SampleRate != targetSampleRate)
        {
            samples = Resample(samples, targetChannels, source.SampleRate, targetSampleRate);
        }

        return samples;
    }
    #endregion
}
