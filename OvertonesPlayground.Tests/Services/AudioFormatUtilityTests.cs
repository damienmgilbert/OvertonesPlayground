using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Tests.Services;

public sealed class AudioFormatUtilityTests
{
    private static WavFile Wav(int channels, int sampleRate, params short[] samples) =>
        new() { Channels = (short)channels, SampleRate = sampleRate, BitsPerSample = 16, Samples = samples };

    #region Conform
    [Fact]
    public void Conform_AlreadyMatchingFormat_ReturnsTheSameSamples()
    {
        WavFile source = Wav(2, 44100, 1, 2, 3, 4);

        short[] result = AudioFormatUtility.Conform(source, targetChannels: 2, targetSampleRate: 44100);

        Assert.Same(source.Samples, result);
    }

    [Fact]
    public void Conform_MonoToStereo_DuplicatesEachSampleAcrossBothChannels()
    {
        WavFile source = Wav(1, 8000, 10, -20, 30);

        short[] result = AudioFormatUtility.Conform(source, targetChannels: 2, targetSampleRate: 8000);

        Assert.Equal([10, 10, -20, -20, 30, 30], result);
    }

    [Fact]
    public void Conform_StereoToMono_AveragesEachPair()
    {
        WavFile source = Wav(2, 8000, 100, 300, -100, 100, short.MaxValue, short.MaxValue);

        short[] result = AudioFormatUtility.Conform(source, targetChannels: 1, targetSampleRate: 8000);

        Assert.Equal([200, 0, short.MaxValue], result);
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(3, 1)]
    [InlineData(6, 2)]
    public void Conform_UnsupportedChannelConversion_ThrowsNotSupported(int sourceChannels, int targetChannels)
    {
        WavFile source = Wav(sourceChannels, 8000, new short[sourceChannels * 4]);

        Assert.Throws<NotSupportedException>(() => AudioFormatUtility.Conform(source, targetChannels, 8000));
    }

    [Fact]
    public void Conform_ChannelsAndRateBothDiffer_ConvertsChannelsThenResamples()
    {
        // 4 stereo frames at 8 kHz -> mono -> 16 kHz gives 8 mono frames.
        WavFile source = Wav(2, 8000, 100, 100, 200, 200, 300, 300, 400, 400);

        short[] result = AudioFormatUtility.Conform(source, targetChannels: 1, targetSampleRate: 16000);

        Assert.Equal(8, result.Length);
        Assert.Equal(100, result[0]);
        Assert.Equal(150, result[1]); // midway between the first two source frames
    }
    #endregion

    #region Resample
    [Fact]
    public void Resample_DoublingTheRate_InterpolatesMidpointsAndHoldsTheLastSample()
    {
        short[] result = AudioFormatUtility.Resample([0, 1000], channels: 1, sourceRate: 100, targetRate: 200);

        Assert.Equal([0, 500, 1000, 1000], result);
    }

    [Fact]
    public void Resample_HalvingTheRate_KeepsEverySecondFrame()
    {
        short[] result = AudioFormatUtility.Resample([0, 100, 200, 300], channels: 1, sourceRate: 200, targetRate: 100);

        Assert.Equal([0, 200], result);
    }

    [Fact]
    public void Resample_SameRate_ReturnsAnEqualCopy()
    {
        short[] source = [5, -5, 7, -7];

        short[] result = AudioFormatUtility.Resample(source, channels: 1, sourceRate: 8000, targetRate: 8000);

        Assert.Equal(source, result);
    }

    [Fact]
    public void Resample_Stereo_InterpolatesEachChannelIndependently()
    {
        // Left ramps 0 -> 1000 while right holds at -200.
        short[] result = AudioFormatUtility.Resample([0, -200, 1000, -200], channels: 2, sourceRate: 100, targetRate: 200);

        Assert.Equal([0, -200, 500, -200, 1000, -200, 1000, -200], result);
    }

    [Fact]
    public void Resample_ResultLengthScalesWithTheRateRatio()
    {
        short[] result = AudioFormatUtility.Resample(new short[4410], channels: 1, sourceRate: 44100, targetRate: 8000);

        Assert.Equal(800, result.Length);
    }

    [Fact]
    public void Resample_NoSamples_ReturnsNoSamples()
    {
        short[] result = AudioFormatUtility.Resample([], channels: 1, sourceRate: 8000, targetRate: 16000);

        Assert.Empty(result);
    }
    #endregion
}
