using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Tests.Services;

public sealed class DrumSynthesizerTests
{
    public static TheoryData<DrumType> AllDrums() => [.. Enum.GetValues<DrumType>()];

    private static short[] Synthesize(DrumType drum, DrumSynthParameters parameters) => drum switch
    {
        DrumType.Kick => DrumSynthesizer.Kick(parameters),
        DrumType.Snare => DrumSynthesizer.Snare(parameters),
        DrumType.HiHat => DrumSynthesizer.HiHat(parameters),
        DrumType.Clap => DrumSynthesizer.Clap(parameters),
        DrumType.Bass => DrumSynthesizer.Bass(parameters),
        DrumType.Tom => DrumSynthesizer.Tom(parameters),
        DrumType.OpenHiHat => DrumSynthesizer.OpenHiHat(parameters),
        DrumType.Rimshot => DrumSynthesizer.Rimshot(parameters),
        DrumType.Crash => DrumSynthesizer.Crash(parameters),
        DrumType.Shaker => DrumSynthesizer.Shaker(parameters),
        _ => throw new ArgumentOutOfRangeException(nameof(drum)),
    };

    private static int ZeroCrossings(short[] samples, int from, int to)
    {
        int crossings = 0;
        for (int i = from + 1; i < to; i++)
        {
            if (Math.Sign(samples[i - 1]) != Math.Sign(samples[i]) && samples[i] != 0)
            {
                crossings++;
            }
        }

        return crossings;
    }

    [Theory]
    [MemberData(nameof(AllDrums))]
    public void EveryDrum_HasItsConfiguredLengthAndIsNotSilent(DrumType drum)
    {
        DrumSynthParameters parameters = DrumSynthParameters.CreateDefault(drum);

        short[] samples = Synthesize(drum, parameters);

        Assert.Equal((int)(parameters.DurationSeconds * parameters.SampleRate), samples.Length);
        Assert.Contains(samples, sample => Math.Abs((int)sample) > 500);
    }

    [Theory]
    [MemberData(nameof(AllDrums))]
    public void EveryDrum_DiesAway(DrumType drum)
    {
        short[] samples = Synthesize(drum, DrumSynthParameters.CreateDefault(drum));
        int fifth = samples.Length / 5;

        double start = TestSignals.Rms(samples[..fifth]);
        double end = TestSignals.Rms(samples[^fifth..]);

        Assert.True(end < start * 0.5, $"{drum}: the last fifth ({end:F4}) should be well under the first ({start:F4})");
    }

    [Theory]
    [MemberData(nameof(AllDrums))]
    public void EveryDrum_StaysWithinTheAmplitudeItWasGiven_ExceptForFilterRinging(DrumType drum)
    {
        DrumSynthParameters parameters = DrumSynthParameters.CreateDefault(drum);
        parameters.FilterType = FilterType.None;
        parameters.Amplitude = 0.4;

        short[] samples = Synthesize(drum, parameters);

        // Noise-based drums layer several sources, so allow headroom above the single-source amplitude but never full scale.
        Assert.True(samples.Max(sample => Math.Abs((int)sample)) < 0.4 * short.MaxValue * 2.5, $"{drum} peaked at {samples.Max(sample => Math.Abs((int)sample))}");
    }

    [Theory]
    [MemberData(nameof(AllDrums))]
    public void EveryDrum_ZeroAmplitude_IsSilent(DrumType drum)
    {
        DrumSynthParameters parameters = DrumSynthParameters.CreateDefault(drum);
        parameters.Amplitude = 0;

        Assert.All(Synthesize(drum, parameters), sample => Assert.Equal(0, sample));
    }

    [Theory]
    [MemberData(nameof(AllDrums))]
    public void EveryDrum_ZeroDuration_IsEmpty(DrumType drum)
    {
        DrumSynthParameters parameters = DrumSynthParameters.CreateDefault(drum);
        parameters.DurationSeconds = 0;

        Assert.Empty(Synthesize(drum, parameters));
    }

    [Theory]
    [InlineData(8_000)]
    [InlineData(22_050)]
    [InlineData(48_000)]
    public void SampleRate_SetsHowManySamplesTheSameDurationTakes(int sampleRate)
    {
        DrumSynthParameters parameters = DrumSynthParameters.CreateDefault(DrumType.Snare);
        parameters.SampleRate = sampleRate;

        Assert.Equal((int)(parameters.DurationSeconds * sampleRate), DrumSynthesizer.Snare(parameters).Length);
    }

    [Theory]
    [InlineData(DrumType.Kick)]
    [InlineData(DrumType.Bass)]
    [InlineData(DrumType.Tom)]
    public void PitchSweptDrums_AreExactlyRepeatable(DrumType drum)
    {
        DrumSynthParameters parameters = DrumSynthParameters.CreateDefault(drum);

        Assert.Equal(Synthesize(drum, parameters), Synthesize(drum, parameters));
    }

    [Theory]
    [InlineData(DrumType.Snare)]
    [InlineData(DrumType.HiHat)]
    [InlineData(DrumType.Clap)]
    [InlineData(DrumType.Crash)]
    public void NoiseDrums_DifferEveryTimeTheyArePlayed(DrumType drum)
    {
        DrumSynthParameters parameters = DrumSynthParameters.CreateDefault(drum);

        Assert.NotEqual(Synthesize(drum, parameters), Synthesize(drum, parameters));
    }

    [Fact]
    public void Kick_StartsHighAndSweepsDownInPitch()
    {
        DrumSynthParameters parameters = DrumSynthParameters.CreateDefault(DrumType.Kick); // 190 Hz falling to 40 Hz
        short[] samples = DrumSynthesizer.Kick(parameters);
        int rate = parameters.SampleRate;

        // Counting zero crossings over equal spans measures the pitch: the opening 50 ms is far higher than the tail.
        int earlyRate = ZeroCrossings(samples, 0, rate / 20);
        int lateRate = ZeroCrossings(samples, samples.Length - (rate / 20) - 1, samples.Length);

        Assert.True(earlyRate > lateRate * 2, $"early {earlyRate} crossings vs late {lateRate}");
    }

    [Fact]
    public void Kick_WithNoSweep_HoldsOneSteadyPitch()
    {
        DrumSynthParameters parameters = DrumSynthParameters.CreateDefault(DrumType.Kick);
        parameters.StartFrequencyHz = 100;
        parameters.EndFrequencyHz = 100;
        parameters.DecayRate = 0;
        short[] samples = DrumSynthesizer.Kick(parameters);
        int rate = parameters.SampleRate;

        int first = ZeroCrossings(samples, 0, rate / 20);
        int second = ZeroCrossings(samples, rate / 10, (rate / 10) + (rate / 20));

        Assert.InRange(Math.Abs(first - second), 0, 1);
        Assert.InRange(first, 8, 12); // 100 Hz for 50 ms is 5 cycles: about 10 zero crossings
    }

    [Fact]
    public void Filter_HighPassOnALowDrum_RemovesMostOfItsEnergy()
    {
        DrumSynthParameters open = DrumSynthParameters.CreateDefault(DrumType.Kick);
        DrumSynthParameters filtered = DrumSynthParameters.CreateDefault(DrumType.Kick);
        filtered.FilterType = FilterType.HighPass;
        filtered.FilterCutoffHz = 8000;

        double before = TestSignals.Rms(DrumSynthesizer.Kick(open));
        double after = TestSignals.Rms(DrumSynthesizer.Kick(filtered));

        Assert.True(after < before * 0.1, $"an 8 kHz high-pass left {after / before:P0} of a kick's level");
    }

    [Fact]
    public void Clap_MoreBurstsMeansMoreEnergyLaterOn()
    {
        DrumSynthParameters single = DrumSynthParameters.CreateDefault(DrumType.Clap);
        single.BurstCount = 1;
        DrumSynthParameters many = DrumSynthParameters.CreateDefault(DrumType.Clap);
        many.BurstCount = 6;
        many.BurstSpacingSeconds = 0.02;
        int from = single.SampleRate / 25; // after the first burst's opening 40 ms

        double singleTail = TestSignals.Rms(DrumSynthesizer.Clap(single)[from..(from * 2)]);
        double manyTail = TestSignals.Rms(DrumSynthesizer.Clap(many)[from..(from * 2)]);

        Assert.True(manyTail > singleTail, $"6 bursts left {manyTail:F4} in the tail, 1 burst {singleTail:F4}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Clap_NoBurstsAskedFor_StillClapsOnce(int burstCount)
    {
        DrumSynthParameters parameters = DrumSynthParameters.CreateDefault(DrumType.Clap);
        parameters.BurstCount = burstCount;

        Assert.Contains(DrumSynthesizer.Clap(parameters), sample => Math.Abs((int)sample) > 500);
    }

    [Fact]
    public void Clap_BurstsSpacedBeyondTheClip_DoNotRunOffTheEnd()
    {
        DrumSynthParameters parameters = DrumSynthParameters.CreateDefault(DrumType.Clap);
        parameters.BurstCount = 20;
        parameters.BurstSpacingSeconds = 5;

        short[] samples = DrumSynthesizer.Clap(parameters);

        Assert.Equal((int)(parameters.DurationSeconds * parameters.SampleRate), samples.Length);
    }
}
