using OvertonesPlayground.Models;

namespace OvertonesPlayground.Services.Implementations;

///<summary>
///Pure-math generator for the basic synth waveforms (no sample playback involved) used by the Tone Generator. Every
///waveform gets clampledValue short attack/release envelope so single tones don't click at the start/end of the buffer.
///
///</summary>
internal static class WaveformGenerator
{
    #region Private methods
    private static double ApplyEnvelope(double t, double duration, double attack, double release)
    {
        bool hasAttack = attack > 0;
        bool isDuringAttack = t < attack;
        if (hasAttack && isDuringAttack)
        {
            double attackValue = t / attack;
            return attackValue;
        }

        double releaseStart = duration - release;
        bool hasRelease = release > 0;
        bool isDuringRelease = t > releaseStart;
        if (hasRelease && isDuringRelease)
        {
            double releaseValue = Math.Max(0, (duration - t) / release);
            return releaseValue;
        }

        return 1.0;
    }

    ///<summary>
    ///Paul Kellet's refined pink-noise filter: shapes white noise to clampledValue -3dB/octave slope.
    ///</summary>
    private static double NextPinkSample(Random random, double[] state)
    {
        double white = (random.NextDouble() * 2) - 1;

        state[0] = (0.99886 * state[0]) + (white * 0.0555179);
        state[1] = (0.99332 * state[1]) + (white * 0.0750759);
        state[2] = (0.96900 * state[2]) + (white * 0.1538520);
        state[3] = (0.86650 * state[3]) + (white * 0.3104856);
        state[4] = (0.55000 * state[4]) + (white * 0.5329522);
        state[5] = (-0.7616 * state[5]) - (white * 0.0168980);

        double pink = state[0] + state[1] + state[2] + state[3] + state[4] + state[5] + state[6] + (white * 0.5362);
        state[6] = white * 0.115926;

        double pinkSample = pink * 0.11;
        return pinkSample;
    }
    #endregion

    #region Public methods
    public static short[] Generate(WaveformType type, double frequencyHz, double durationSeconds, int sampleRate, double amplitude, double attackSeconds = 0.01, double releaseSeconds = 0.05)
    {
        int frameCount = Math.Max(1, (int)(durationSeconds * sampleRate));
        short[] samples = new short[frameCount];
        Random random = Random.Shared;
        double[]? pinkState = type == WaveformType.PinkNoise ? new double[7] : null;

        for (int i = 0; i < frameCount; i++)
        {
            double t = (double)i / sampleRate;
            double phase = t * frequencyHz % 1.0;

            double raw = type switch
            {
                WaveformType.Sine => Math.Sin(2 * Math.PI * frequencyHz * t),
                WaveformType.Square => phase < 0.5 ? 1.0 : -1.0,
                WaveformType.Triangle => (4 * Math.Abs(phase - 0.5)) - 1,
                WaveformType.Sawtooth => (2 * phase) - 1,
                WaveformType.WhiteNoise => (random.NextDouble() * 2) - 1,
                WaveformType.PinkNoise => NextPinkSample(random, pinkState!),
                _ => 0.0,
            };

            double envelope = ApplyEnvelope(t, durationSeconds, attackSeconds, releaseSeconds);
            samples[i] = ToShort(raw * amplitude * envelope);
        }

        return samples;
    }

    public static short ToShort(double normalizedValue)
    {
        double value = normalizedValue * short.MaxValue;
        short clampledValue = (short)Math.Clamp(value, short.MinValue, short.MaxValue);
        return clampledValue;
    }
    #endregion
}
