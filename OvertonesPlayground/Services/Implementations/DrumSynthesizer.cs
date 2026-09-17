namespace OvertonesPlayground.Services.Implementations;

/// <summary>
/// Small library of classic drum-machine one-shots, synthesized from scratch (sine sweeps and
/// shaped noise) rather than sampled - so the Tone Generator can produce a full kit with no
/// bundled audio assets.
/// </summary>
internal static class DrumSynthesizer
{
    public static short[] Kick(int sampleRate) =>
        PitchSweep(sampleRate, duration: 0.35, startFreq: 190, endFreq: 40, sweepRate: 18, decayRate: 9, amplitude: 0.9);

    public static short[] Bass(int sampleRate, double frequencyHz = 80) =>
        PitchSweep(sampleRate, duration: 0.45, startFreq: frequencyHz * 1.5, endFreq: frequencyHz, sweepRate: 25, decayRate: 4, amplitude: 0.8);

    public static short[] Snare(int sampleRate)
    {
        const double duration = 0.2;
        var frameCount = (int)(duration * sampleRate);
        var buffer = new double[frameCount];
        var random = Random.Shared;
        double phase = 0;

        for (var i = 0; i < frameCount; i++)
        {
            var t = (double)i / sampleRate;
            phase += 2 * Math.PI * 180 / sampleRate;

            var tone = Math.Sin(phase) * Math.Exp(-t * 30) * 0.4;
            var noise = ((random.NextDouble() * 2) - 1) * Math.Exp(-t * 18) * 0.9;
            buffer[i] = tone + noise;
        }

        return ToShortArray(buffer);
    }

    public static short[] HiHat(int sampleRate)
    {
        const double duration = 0.08;
        var frameCount = (int)(duration * sampleRate);
        var buffer = new double[frameCount];
        var random = Random.Shared;
        double previous = 0;

        for (var i = 0; i < frameCount; i++)
        {
            var t = (double)i / sampleRate;
            var noise = (random.NextDouble() * 2) - 1;
            var highPassed = noise - previous; // crude high-pass: emphasize the noise's high-frequency content
            previous = noise;

            buffer[i] = highPassed * Math.Exp(-t * 40) * 0.6;
        }

        return ToShortArray(buffer);
    }

    public static short[] Clap(int sampleRate)
    {
        const double duration = 0.25;
        var frameCount = (int)(duration * sampleRate);
        var buffer = new double[frameCount];
        var random = Random.Shared;

        foreach (var burstStart in (double[])[0, 0.01, 0.02, 0.035])
        {
            var startFrame = (int)(burstStart * sampleRate);
            for (var i = 0; i < frameCount - startFrame; i++)
            {
                var t = (double)i / sampleRate;
                var amp = Math.Exp(-t * 25);
                buffer[startFrame + i] += ((random.NextDouble() * 2) - 1) * amp * 0.5;
            }
        }

        return ToShortArray(buffer);
    }

    private static short[] PitchSweep(
        int sampleRate, double duration, double startFreq, double endFreq, double sweepRate, double decayRate, double amplitude)
    {
        var frameCount = (int)(duration * sampleRate);
        var samples = new short[frameCount];
        double phase = 0;

        for (var i = 0; i < frameCount; i++)
        {
            var t = (double)i / sampleRate;
            var freq = ((startFreq - endFreq) * Math.Exp(-t * sweepRate)) + endFreq;
            phase += 2 * Math.PI * freq / sampleRate;

            var amp = Math.Exp(-t * decayRate) * amplitude;
            samples[i] = WaveformGenerator.ToShort(Math.Sin(phase) * amp);
        }

        return samples;
    }

    private static short[] ToShortArray(double[] buffer)
    {
        var samples = new short[buffer.Length];
        for (var i = 0; i < buffer.Length; i++)
        {
            samples[i] = WaveformGenerator.ToShort(buffer[i]);
        }

        return samples;
    }
}
