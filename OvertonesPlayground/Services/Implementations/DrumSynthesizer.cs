using OvertonesPlayground.Models;

namespace OvertonesPlayground.Services.Implementations;

/// <summary>
/// Small library of classic drum-machine one-shots, synthesized from scratch (sine sweeps and
/// shaped noise) rather than sampled - so the Tone Generator can produce a full kit with no
/// bundled audio assets. Every parameter is driven by <see cref="DrumSynthParameters"/> so the
/// user has full control; a post-processing <see cref="BiquadFilter"/> pass is applied last.
/// </summary>
internal static class DrumSynthesizer
{
    /// <summary>Pitch-swept low thump.</summary>
    public static short[] Kick(DrumSynthParameters p) => PitchSweep(p);

    /// <summary>Pitch-swept sustained low tone.</summary>
    public static short[] Bass(DrumSynthParameters p) => PitchSweep(p);

    /// <summary>Pitch-swept mid-range drum.</summary>
    public static short[] Tom(DrumSynthParameters p) => PitchSweep(p);

    /// <summary>Layered tone-and-noise snare body.</summary>
    public static short[] Snare(DrumSynthParameters p) => ToneAndNoise(p);

    /// <summary>Short, high-pitched tone-and-noise hit.</summary>
    public static short[] Rimshot(DrumSynthParameters p) => ToneAndNoise(p);

    /// <summary>Short decaying filtered noise.</summary>
    public static short[] HiHat(DrumSynthParameters p) => DecayingNoise(p);

    /// <summary>Longer-decaying filtered noise.</summary>
    public static short[] OpenHiHat(DrumSynthParameters p) => DecayingNoise(p);

    /// <summary>Long, slow-decaying filtered noise.</summary>
    public static short[] Crash(DrumSynthParameters p) => DecayingNoise(p);

    /// <summary>Very short decaying filtered noise.</summary>
    public static short[] Shaker(DrumSynthParameters p) => DecayingNoise(p);

    /// <summary>Shared generator for Snare/Rimshot: a decaying sine tone layered under decaying noise.</summary>
    private static short[] ToneAndNoise(DrumSynthParameters p)
    {
        var frameCount = (int)(p.DurationSeconds * p.SampleRate);
        var buffer = new double[frameCount];
        var random = Random.Shared;
        double phase = 0;

        for (var i = 0; i < frameCount; i++)
        {
            var t = (double)i / p.SampleRate;
            phase += 2 * Math.PI * p.ToneFrequencyHz / p.SampleRate;

            var tone = Math.Sin(phase) * Math.Exp(-t * p.ToneDecayRate) * p.ToneLevel;
            var noise = ((random.NextDouble() * 2) - 1) * Math.Exp(-t * p.DecayRate) * p.NoiseLevel;
            buffer[i] = (tone + noise) * p.Amplitude;
        }

        return Finalize(buffer, p);
    }

    /// <summary>Shared generator for HiHat/OpenHiHat/Crash/Shaker: white noise under an exponential decay envelope.</summary>
    private static short[] DecayingNoise(DrumSynthParameters p)
    {
        var frameCount = (int)(p.DurationSeconds * p.SampleRate);
        var buffer = new double[frameCount];
        var random = Random.Shared;

        for (var i = 0; i < frameCount; i++)
        {
            var t = (double)i / p.SampleRate;
            var noise = (random.NextDouble() * 2) - 1;
            buffer[i] = noise * Math.Exp(-t * p.DecayRate) * p.Amplitude;
        }

        return Finalize(buffer, p);
    }

    /// <summary>Several overlapping decaying noise bursts, spaced <see cref="DrumSynthParameters.BurstSpacingSeconds"/> apart.</summary>
    public static short[] Clap(DrumSynthParameters p)
    {
        var frameCount = (int)(p.DurationSeconds * p.SampleRate);
        var buffer = new double[frameCount];
        var random = Random.Shared;

        for (var burst = 0; burst < Math.Max(1, p.BurstCount); burst++)
        {
            var startFrame = (int)(burst * p.BurstSpacingSeconds * p.SampleRate);
            for (var i = 0; i < frameCount - startFrame; i++)
            {
                var t = (double)i / p.SampleRate;
                var amp = Math.Exp(-t * p.DecayRate);
                buffer[startFrame + i] += ((random.NextDouble() * 2) - 1) * amp * p.Amplitude;
            }
        }

        return Finalize(buffer, p);
    }

    /// <summary>Shared generator for Kick/Bass/Tom: a sine oscillator whose frequency decays exponentially from start to end.</summary>
    private static short[] PitchSweep(DrumSynthParameters p)
    {
        var frameCount = (int)(p.DurationSeconds * p.SampleRate);
        var buffer = new double[frameCount];
        double phase = 0;

        for (var i = 0; i < frameCount; i++)
        {
            var t = (double)i / p.SampleRate;
            var freq = ((p.StartFrequencyHz - p.EndFrequencyHz) * Math.Exp(-t * p.SweepRate)) + p.EndFrequencyHz;
            phase += 2 * Math.PI * freq / p.SampleRate;

            var amp = Math.Exp(-t * p.DecayRate) * p.Amplitude;
            buffer[i] = Math.Sin(phase) * amp;
        }

        return Finalize(buffer, p);
    }

    /// <summary>Converts a double-precision buffer to 16-bit PCM and applies the parameters' post-processing filter.</summary>
    private static short[] Finalize(double[] buffer, DrumSynthParameters p)
    {
        var samples = new short[buffer.Length];
        for (var i = 0; i < buffer.Length; i++)
        {
            samples[i] = WaveformGenerator.ToShort(buffer[i]);
        }

        BiquadFilter.Apply(samples, p.SampleRate, p.FilterType, p.FilterCutoffHz, p.FilterResonance);
        return samples;
    }
}
