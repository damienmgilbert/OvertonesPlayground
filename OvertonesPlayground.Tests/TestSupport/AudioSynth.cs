namespace OvertonesPlayground.Tests.TestSupport;

///<summary>
///Synthetic floating-point test signals with known properties (frequency, level, tempo, decay), plus helpers that wrap
///them in the analysis types. Everything is deterministic: noise uses a fixed-seed generator.
///</summary>
public static class AudioSynth
{
    #region Constants

    ///<summary>
    ///Default sample rate of the synthetic signals.
    ///</summary>
    public const int Rate = 44100;
    #endregion

    #region Public methods
    ///<summary>
    ///A short decaying burst of noise every beat, like a drum loop reduced to its pulse.
    ///</summary>
    public static float[] ClickTrain(double bpm, double seconds, int sampleRate = Rate, double amplitude = 0.8)
    {
        float[] signal = new float[(int)(seconds * sampleRate)];
        double beatSamples = 60.0 / bpm * sampleRate;
        Random random = new(7);
        int burst = (int)(0.02 * sampleRate);
        for (double start = 0; start < signal.Length; start += beatSamples)
        {
            for (int i = 0; i < burst && (int)start + i < signal.Length; i++)
            {
                double envelope = Math.Exp(-6.0 * i / burst);
                signal[(int)start + i] = (float)(amplitude * envelope * ((random.NextDouble() * 2) - 1));
            }
        }

        return signal;
    }

    ///<summary>
    ///Concatenates signals end to end.
    ///</summary>
    public static float[] Concat(params float[][] parts) => [.. parts.SelectMany(part => part)];

    ///<summary>
    ///Wraps mono or multi-channel signals in an <see cref="AnalysisContext"/> as if read from a 24-bit PCM file.
    ///</summary>
    public static AnalysisContext Context(int sampleRate, NamedAttributes? named = null, params float[][] channels)
    {
        AudioBuffer audio = new(sampleRate, channels);
        WavFormat format = new(channels.Length, sampleRate, 24, false, false, false);
        WavData wav = new(audio, RiffMetadata.Empty, format);
        return new AnalysisContext(wav, 1000, named ?? NamedAttributes.Empty("test"));
    }

    ///<summary>
    ///Exponential decay of <paramref name="decayDbPerSecond"/> dB per second.
    ///</summary>
    public static float[] Decay(float[] signal, double decayDbPerSecond, int sampleRate = Rate) => Shape(signal, t => Math.Pow(10.0, -decayDbPerSecond * t / 20.0), sampleRate);

    ///<summary>
    ///A stack of <paramref name="harmonics"/> harmonics with amplitude 1/k.
    ///</summary>
    public static float[] Harmonic(double fundamentalHz, int harmonics, double seconds, int sampleRate = Rate, double amplitude = 0.5)
    {
        float[] signal = new float[(int)(seconds * sampleRate)];
        double norm = 0;
        for (int k = 1; k <= harmonics; k++)
        {
            norm += 1.0 / k;
        }

        for (int k = 1; k <= harmonics; k++)
        {
            for (int i = 0; i < signal.Length; i++)
            {
                signal[i] += (float)(amplitude / norm / k * Math.Sin(2 * Math.PI * k * fundamentalHz * i / sampleRate));
            }
        }

        return signal;
    }

    ///<summary>
    ///Negated copy of <paramref name="signal"/> (polarity inversion).
    ///</summary>
    public static float[] Invert(float[] signal) => [.. signal.Select(sample => -sample)];

    ///<summary>
    ///Mono context at 44.1 kHz.
    ///</summary>
    public static AnalysisContext Mono(float[] signal, NamedAttributes? named = null) => Context(Rate, named, signal);

    ///<summary>
    ///Uniform white noise from a fixed seed.
    ///</summary>
    public static float[] Noise(double seconds, int sampleRate = Rate, double amplitude = 0.5, int seed = 1)
    {
        Random random = new(seed);
        float[] signal = new float[(int)(seconds * sampleRate)];
        for (int i = 0; i < signal.Length; i++)
        {
            signal[i] = (float)(amplitude * ((random.NextDouble() * 2) - 1));
        }

        return signal;
    }

    ///<summary>
    ///Multiplies <paramref name="signal"/> by a gain that depends on time in seconds.
    ///</summary>
    public static float[] Shape(float[] signal, Func<double, double> gainAtSeconds, int sampleRate = Rate)
    {
        float[] output = new float[signal.Length];
        for (int i = 0; i < signal.Length; i++)
        {
            output[i] = (float)(signal[i] * gainAtSeconds((double)i / sampleRate));
        }

        return output;
    }

    ///<summary>
    ///Digital silence.
    ///</summary>
    public static float[] Silence(double seconds, int sampleRate = Rate) => new float[(int)(seconds * sampleRate)];

    ///<summary>
    ///A sine wave.
    ///</summary>
    public static float[] Sine(double frequencyHz, double seconds, int sampleRate = Rate, double amplitude = 0.5, double phase = 0)
    {
        float[] signal = new float[(int)(seconds * sampleRate)];
        for (int i = 0; i < signal.Length; i++)
        {
            signal[i] = (float)(amplitude * Math.Sin((2 * Math.PI * frequencyHz * i / sampleRate) + phase));
        }

        return signal;
    }
    #endregion
}
