using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Tests.Services;

public sealed class SpectralNoiseReducerTests
{
    #region Constants
    private const int Rate = 44_100;
    #endregion

    #region Private methods
    ///<summary>
    ///One second of steady noise with a 1 kHz tone added from 0.3 s on, so the first stretch is noise alone (what a
    ///noise profile is taken from) and the rest is a wanted sound sitting on top of it.
    ///</summary>
    private static short[] NoiseWithATone(int noiseAmplitude = 800, int toneAmplitude = 8000, int seed = 11)
    {
        Random random = new(seed);
        short[] samples = new short[Rate];
        for (int i = 0; i < samples.Length; i++)
        {
            double noise = random.Next(-noiseAmplitude, noiseAmplitude + 1);
            double tone = i >= Rate * 3 / 10 ? Math.Sin(2 * Math.PI * 1000 * i / Rate) * toneAmplitude : 0;
            samples[i] = (short)(noise + tone);
        }

        return samples;
    }
    #endregion

    #region Public methods
    [Fact]
    public void Reduce_ClipShorterThanOneAnalysisWindow_IsNotSilenced()
    {
        // One analysis window is 2048 samples (46 ms): a shorter clip must not vanish.
        short[] input = WavTestFiles.Constant(10_000, 1500);

        short[] output = SpectralNoiseReducer.Reduce(input, channels: 1, noiseFrameCount: 0);

        Assert.Contains(output, sample => sample != 0);
    }

    [Fact]
    public void Reduce_DoesNotChangeTheInput()
    {
        short[] input = NoiseWithATone();
        short[] copy = (short[])input.Clone();

        _ = SpectralNoiseReducer.Reduce(input, 1, Rate / 4);

        Assert.Equal(copy, input);
    }

    [Fact]
    public void Reduce_EachChannelIsCleanedByItsOwnProfile()
    {
        // Left is noisy, right is silent: the silent channel must come out silent, untouched by the left's noise profile.
        short[] noisy = NoiseWithATone();
        short[] stereo = new short[noisy.Length * 2];
        for (int i = 0; i < noisy.Length; i++)
        {
            stereo[i * 2] = noisy[i];
        }

        short[] output = SpectralNoiseReducer.Reduce(stereo, channels: 2, noiseFrameCount: Rate / 4);

        Assert.Equal(stereo.Length, output.Length);
        for (int i = 1; i < output.Length; i += 2)
        {
            Assert.Equal(0, output[i]);
        }

        double before = TestSignals.Rms([.. Enumerable.Range(3000, 6000).Select(i => noisy[i])]);
        double after = TestSignals.Rms([.. Enumerable.Range(3000, 6000).Select(i => output[i * 2])]);
        Assert.True(after < before * 0.4);
    }

    [Fact]
    public void Reduce_KeepsTheLengthOfTheClip()
    {
        short[] input = NoiseWithATone();

        Assert.Equal(input.Length, SpectralNoiseReducer.Reduce(input, 1, Rate / 4).Length);
    }

    [Fact]
    public void Reduce_KeepsTheWantedSoundThatSitsOnTopOfTheNoise()
    {
        short[] input = NoiseWithATone();
        double toneLevel = 8000 / Math.Sqrt(2) / short.MaxValue; // RMS of the 8000-peak tone

        short[] output = SpectralNoiseReducer.Reduce(input, channels: 1, noiseFrameCount: Rate / 4);

        double after = TestSignals.Rms(output[18000..40000]);
        Assert.InRange(after, toneLevel * 0.8, toneLevel * 1.1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    [InlineData(10_000_000)]
    public void Reduce_NoiseSampleOutsideTheClip_IsClampedInsteadOfFailing(int noiseFrameCount)
    {
        short[] input = NoiseWithATone();

        short[] output = SpectralNoiseReducer.Reduce(input, 1, noiseFrameCount);

        Assert.Equal(input.Length, output.Length);
    }

    [Fact]
    public void Reduce_NoNoiseProfile_LeavesTheMiddleOfASteadySignalAsItWas()
    {
        short[] steady = WavTestFiles.Constant(10_000, Rate / 2);

        short[] output = SpectralNoiseReducer.Reduce(steady, channels: 1, noiseFrameCount: 0);

        Assert.All(output[3000..18000], sample => Assert.InRange(sample, 9_990, 10_010));
    }

    [Fact]
    public void Reduce_NothingToProcess_ReturnsItAsItIs()
    {
        short[] empty = [];

        Assert.Same(empty, SpectralNoiseReducer.Reduce(empty, channels: 1, noiseFrameCount: 100));
        short[] some = [1, 2, 3];
        Assert.Same(some, SpectralNoiseReducer.Reduce(some, channels: 0, noiseFrameCount: 100));
    }

    [Fact]
    public void Reduce_Silence_StaysSilent()
    {
        short[] output = SpectralNoiseReducer.Reduce(new short[Rate / 2], channels: 1, noiseFrameCount: 5000);

        Assert.All(output, sample => Assert.Equal(0, sample));
    }

    [Fact]
    public void Reduce_TakesTheHissOutOfTheNoiseOnlyStretch()
    {
        short[] input = NoiseWithATone();

        short[] output = SpectralNoiseReducer.Reduce(input, channels: 1, noiseFrameCount: Rate / 4);

        // Compare well inside the noise-only stretch, away from the clip's edges.
        double before = TestSignals.Rms(input[3000..9000]);
        double after = TestSignals.Rms(output[3000..9000]);
        Assert.True(after < before * 0.4, $"noise went from {before:F1}... to {after:F1}... of full scale; expected well under 40%");
    }

    [Fact]
    public void Reduce_TheEndOfTheClip_IsKeptRatherThanCutOff()
    {
        // The sound continues right to the end of the clip; whatever the last partial window would have covered must still play.
        short[] input = WavTestFiles.Constant(10_000, (Rate / 2) + 700);

        short[] output = SpectralNoiseReducer.Reduce(input, channels: 1, noiseFrameCount: 0);

        Assert.All(output[^600..^100], sample => Assert.InRange(sample, 9_000, 11_000));
    }
    #endregion
}
