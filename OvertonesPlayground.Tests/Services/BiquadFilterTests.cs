using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Tests.Services;

public sealed class BiquadFilterTests
{
    private const int SampleRate = 44100;

    /// <summary>
    /// Kept low so a +12 dB boost (about x4) stays under full scale; a hotter tone would clip and understate the gain.
    /// </summary>
    private const double ToneAmplitude = 0.1;

    /// <summary>
    /// Level of a pure tone after the filter, measured over the back half of the buffer to skip the start-up transient.
    /// </summary>
    private static double LevelAfter(double toneHz, Action<short[]> filter, int channels = 1)
    {
        short[] samples = TestSignals.Sine(toneHz, SampleRate, seconds: 0.5, amplitude: ToneAmplitude, channels: channels);
        filter(samples);
        return TestSignals.Rms(samples, startIndex: samples.Length / 2);
    }

    private static double LevelBefore(double toneHz, int channels = 1) =>
        TestSignals.Rms(TestSignals.Sine(toneHz, SampleRate, 0.5, ToneAmplitude, channels), startIndex: SampleRate / 4 * channels);

    #region Apply (low-pass / high-pass / band-pass)
    [Fact]
    public void Apply_LowPass_PassesLowTonesAndAttenuatesHighOnes()
    {
        void Filter(short[] s) => BiquadFilter.Apply(s, SampleRate, FilterType.LowPass, cutoffHz: 1000, resonanceQ: 0.707);

        double low = LevelAfter(100, Filter) / LevelBefore(100);
        double high = LevelAfter(10000, Filter) / LevelBefore(10000);

        Assert.InRange(low, 0.95, 1.05);
        Assert.True(high < 0.05, $"a 10 kHz tone through a 1 kHz low-pass kept {high:P1} of its level");
    }

    [Fact]
    public void Apply_HighPass_PassesHighTonesAndAttenuatesLowOnes()
    {
        void Filter(short[] s) => BiquadFilter.Apply(s, SampleRate, FilterType.HighPass, cutoffHz: 1000, resonanceQ: 0.707);

        double low = LevelAfter(50, Filter) / LevelBefore(50);
        double high = LevelAfter(10000, Filter) / LevelBefore(10000);

        Assert.True(low < 0.05, $"a 50 Hz tone through a 1 kHz high-pass kept {low:P1} of its level");
        Assert.InRange(high, 0.95, 1.05);
    }

    [Fact]
    public void Apply_BandPass_FavoursTonesAtTheCutoffOverTonesFarFromIt()
    {
        void Filter(short[] s) => BiquadFilter.Apply(s, SampleRate, FilterType.BandPass, cutoffHz: 1000, resonanceQ: 2);

        double atCentre = LevelAfter(1000, Filter);
        double below = LevelAfter(60, Filter);
        double above = LevelAfter(15000, Filter);

        Assert.True(atCentre > below * 10, $"centre {atCentre:F4} vs below {below:F4}");
        Assert.True(atCentre > above * 10, $"centre {atCentre:F4} vs above {above:F4}");
    }

    [Fact]
    public void Apply_FilterTypeNone_LeavesSamplesUntouched()
    {
        short[] samples = TestSignals.Sine(440, SampleRate, 0.05);
        short[] original = (short[])samples.Clone();

        BiquadFilter.Apply(samples, SampleRate, FilterType.None, cutoffHz: 1000, resonanceQ: 1);

        Assert.Equal(original, samples);
    }

    [Fact]
    public void Apply_EmptyBuffer_DoesNothing()
    {
        BiquadFilter.Apply([], SampleRate, FilterType.LowPass, cutoffHz: 1000, resonanceQ: 1);
    }

    [Theory]
    [InlineData(-500.0, 1.0)]
    [InlineData(1000.0, 0.0)]
    [InlineData(1000.0, -3.0)]
    [InlineData(1_000_000.0, 1.0)]
    public void Apply_OutOfRangeCutoffOrResonance_IsClampedRatherThanBlowingUp(double cutoffHz, double resonanceQ)
    {
        short[] samples = TestSignals.Sine(440, SampleRate, 0.1);

        BiquadFilter.Apply(samples, SampleRate, FilterType.LowPass, cutoffHz, resonanceQ);

        Assert.All(samples, sample => Assert.InRange(sample, short.MinValue, short.MaxValue));
        Assert.True(TestSignals.Rms(samples) < 1, "the output should stay a sane audio signal");
    }
    #endregion

    #region Equalizer bands (peaking / shelves)
    [Fact]
    public void ApplyPeaking_ZeroGain_IsEssentiallyTransparent()
    {
        short[] samples = TestSignals.Sine(1000, SampleRate, 0.1);
        short[] original = (short[])samples.Clone();

        BiquadFilter.ApplyPeaking(samples, channels: 1, SampleRate, frequencyHz: 1000, gainDb: 0, q: 1);

        for (int i = 0; i < samples.Length; i++)
        {
            Assert.InRange(samples[i] - original[i], -2, 2);
        }
    }

    [Fact]
    public void ApplyPeaking_Boost_RaisesTheCentreFrequencyMoreThanADistantOne()
    {
        void Filter(short[] s) => BiquadFilter.ApplyPeaking(s, channels: 1, SampleRate, frequencyHz: 1000, gainDb: 12, q: 2);

        double centreGain = LevelAfter(1000, Filter) / LevelBefore(1000);
        double farGain = LevelAfter(10000, Filter) / LevelBefore(10000);

        Assert.InRange(centreGain, 3.5, 4.5); // +12 dB is about x3.98
        Assert.InRange(farGain, 0.9, 1.1);
    }

    [Fact]
    public void ApplyPeaking_Cut_LowersTheCentreFrequency()
    {
        void Filter(short[] s) => BiquadFilter.ApplyPeaking(s, channels: 1, SampleRate, frequencyHz: 1000, gainDb: -12, q: 2);

        double centreGain = LevelAfter(1000, Filter) / LevelBefore(1000);

        Assert.InRange(centreGain, 0.2, 0.3); // -12 dB is about x0.25
    }

    [Fact]
    public void ApplyLowShelf_Boost_RaisesLowTonesAndLeavesHighOnesAlone()
    {
        void Filter(short[] s) => BiquadFilter.ApplyLowShelf(s, channels: 1, SampleRate, frequencyHz: 500, gainDb: 12, q: 0.707);

        double low = LevelAfter(50, Filter) / LevelBefore(50);
        double high = LevelAfter(10000, Filter) / LevelBefore(10000);

        Assert.InRange(low, 3.5, 4.5);
        Assert.InRange(high, 0.95, 1.05);
    }

    [Fact]
    public void ApplyHighShelf_Boost_RaisesHighTonesAndLeavesLowOnesAlone()
    {
        void Filter(short[] s) => BiquadFilter.ApplyHighShelf(s, channels: 1, SampleRate, frequencyHz: 4000, gainDb: 12, q: 0.707);

        double high = LevelAfter(15000, Filter) / LevelBefore(15000);
        double low = LevelAfter(100, Filter) / LevelBefore(100);

        Assert.InRange(high, 3.5, 4.5);
        Assert.InRange(low, 0.95, 1.05);
    }

    [Fact]
    public void ApplyShelfOrPeak_Stereo_KeepsTheChannelsIndependent()
    {
        // Sound only in the left channel; the right channel's filter history must never pick any of it up.
        short[] left = TestSignals.Sine(1000, SampleRate, 0.1);
        short[] samples = new short[left.Length * 2];
        for (int frame = 0; frame < left.Length; frame++)
        {
            samples[frame * 2] = left[frame];
        }

        BiquadFilter.ApplyPeaking(samples, channels: 2, SampleRate, frequencyHz: 1000, gainDb: 6, q: 1);

        for (int frame = 0; frame < left.Length; frame++)
        {
            Assert.Equal(0, samples[(frame * 2) + 1]);
        }

        Assert.Contains(samples.Where((_, index) => index % 2 == 0), sample => sample != 0);
    }

    [Fact]
    public void ApplyShelfOrPeak_Stereo_FiltersEachChannelLikeAMonoBuffer()
    {
        short[] mono = TestSignals.Sine(1000, SampleRate, 0.1);
        short[] stereo = TestSignals.Sine(1000, SampleRate, 0.1, channels: 2);

        BiquadFilter.ApplyLowShelf(mono, channels: 1, SampleRate, frequencyHz: 500, gainDb: 6, q: 0.707);
        BiquadFilter.ApplyLowShelf(stereo, channels: 2, SampleRate, frequencyHz: 500, gainDb: 6, q: 0.707);

        for (int frame = 0; frame < mono.Length; frame++)
        {
            Assert.Equal(mono[frame], stereo[frame * 2]);
            Assert.Equal(mono[frame], stereo[(frame * 2) + 1]);
        }
    }

    [Fact]
    public void ApplyShelfOrPeak_EmptyBufferOrZeroChannels_DoesNothing()
    {
        BiquadFilter.ApplyPeaking([], channels: 1, SampleRate, 1000, 6, 1);

        short[] samples = TestSignals.Sine(1000, SampleRate, 0.01);
        short[] original = (short[])samples.Clone();
        BiquadFilter.ApplyPeaking(samples, channels: 0, SampleRate, 1000, 6, 1);

        Assert.Equal(original, samples);
    }
    #endregion
}
