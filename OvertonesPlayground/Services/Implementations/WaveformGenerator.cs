using OvertonesPlayground.Models;

namespace OvertonesPlayground.Services.Implementations;

/// <summary>
/// Pure-math generator for the basic synth waveforms (no sample playback involved) used by
/// the Tone Generator. Every waveform gets a short attack/release envelope so single tones
/// don't click at the start/end of the buffer.
/// </summary>
internal static class WaveformGenerator
{
    public static short[] Generate(
        WaveformType type,
        double frequencyHz,
        double durationSeconds,
        int sampleRate,
        double amplitude,
        double attackSeconds = 0.01,
        double releaseSeconds = 0.05)
    {
        var frameCount = Math.Max(1, (int)(durationSeconds * sampleRate));
        var samples = new short[frameCount];
        var random = Random.Shared;

        for (var i = 0; i < frameCount; i++)
        {
            var t = (double)i / sampleRate;
            var phase = (t * frequencyHz) % 1.0;

            var raw = type switch
            {
                WaveformType.Sine => Math.Sin(2 * Math.PI * frequencyHz * t),
                WaveformType.Square => phase < 0.5 ? 1.0 : -1.0,
                WaveformType.Triangle => (4 * Math.Abs(phase - 0.5)) - 1,
                WaveformType.Sawtooth => (2 * phase) - 1,
                WaveformType.WhiteNoise => (random.NextDouble() * 2) - 1,
                _ => 0.0,
            };

            var envelope = ApplyEnvelope(t, durationSeconds, attackSeconds, releaseSeconds);
            samples[i] = ToShort(raw * amplitude * envelope);
        }

        return samples;
    }

    private static double ApplyEnvelope(double t, double duration, double attack, double release)
    {
        if (attack > 0 && t < attack)
        {
            return t / attack;
        }

        var releaseStart = duration - release;
        if (release > 0 && t > releaseStart)
        {
            return Math.Max(0, (duration - t) / release);
        }

        return 1.0;
    }

    public static short ToShort(double normalizedValue) =>
        (short)Math.Clamp(normalizedValue * short.MaxValue, short.MinValue, short.MaxValue);
}
