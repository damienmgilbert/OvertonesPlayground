using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Tests.Services;

public sealed class AudioEditorServiceTests : IDisposable
{
    #region Constants
    private const int Rate = 44100;
    #endregion

    #region Fields
    private readonly TempFileSystem _files = new();
    private readonly AudioEditorService _service;
    #endregion

    #region Constructors
    public AudioEditorServiceTests() { _service = new AudioEditorService(_files); }
    #endregion

    #region Private methods
    private async Task<double> LevelAfterEqualizerAsync(double toneHz, double low, double mid, double high)
    {
        short[] tone = TestSignals.Sine(toneHz, Rate, 0.5, amplitude: 0.1);
        string source = await Source(tone, sampleRate: Rate);
        short[] result = await SamplesOf(await _service.ApplyEqualizerAsync(source, low, mid, high, "eq"));
        return TestSignals.Rms(result, result.Length / 2) / TestSignals.Rms(tone, tone.Length / 2);
    }

    private static async Task<short[]> SamplesOf(string path) => (await WavTestFiles.ReadAsync(path)).Samples;

    private Task<string> Source(short[] samples, int channels = 1, int sampleRate = 1000, string name = "source.wav") => WavTestFiles.WriteAsync(_files.InAppData("in", name), samples, channels, sampleRate);

        // Stereo samples are interleaved left, right, left, right... Numbering them 1, 2, 3, 4... makes every odd value a left sample.
    // A cut that lands on a right sample would start the output on the wrong channel and swap left and right from there on.
    private static bool StartsOnALeftSample(short[] samples) => samples[0] % 2 == 1;
    #endregion

    #region Public methods
    [Fact]
    public async Task ApplyCompression_RatioBelowOne_IsTreatedAsNoCompression()
    {
        short[] loud = WavTestFiles.Constant(short.MaxValue / 2, Rate / 4);
        string source = await Source(loud, sampleRate: Rate);

        short[] result = await SamplesOf(await _service.ApplyCompressionAsync(source, -20, 0.25, 1, 1, "comp"));

        Assert.Equal(loud, result);
    }

    [Fact]
    public async Task ApplyCompression_ReducesALoudSteadySignal()
    {
        string source = await Source(WavTestFiles.Constant(short.MaxValue / 2, Rate / 2), sampleRate: Rate);

        short[] result = await SamplesOf(await _service.ApplyCompressionAsync(source, -20, 4, 1, 1, "comp"));

        Assert.True(result[^1] < short.MaxValue / 4, $"expected heavy reduction, got {result[^1]}");
        Assert.Equal(Rate / 2, result.Length);
    }

    [Fact]
    public async Task ApplyEqualizer_FlatSettings_LeaveTheSoundAsItWas() { Assert.InRange(await LevelAfterEqualizerAsync(1000, 0, 0, 0), 0.98, 1.02); }
    [Fact]
    public async Task ApplyEqualizer_HighCut_LowersTrebleAndLeavesBassAlone()
    {
        Assert.InRange(await LevelAfterEqualizerAsync(12000, 0, 0, -12), 0.2, 0.32);
        Assert.InRange(await LevelAfterEqualizerAsync(100, 0, 0, -12), 0.95, 1.05);
    }

    [Fact]
    public async Task ApplyEqualizer_LowBoost_RaisesBassAndLeavesTrebleAlone()
    {
        Assert.InRange(await LevelAfterEqualizerAsync(50, 12, 0, 0), 3, 4.5);
        Assert.InRange(await LevelAfterEqualizerAsync(12000, 12, 0, 0), 0.95, 1.05);
    }

    [Fact]
    public async Task ApplyEqualizer_MidBoost_RaisesTheMiddleMost()
    {
        Assert.InRange(await LevelAfterEqualizerAsync(1000, 0, 12, 0), 3, 4.5);
        Assert.InRange(await LevelAfterEqualizerAsync(50, 0, 12, 0), 0.9, 1.15);
    }

    [Fact]
    public async Task ApplyFade_LongerThanTheClip_StillFinishesAndKeepsTheLength()
    {
        short[] result = await SamplesOf(await _service.ApplyFadeAsync(await Source(WavTestFiles.Constant(5000, 100)), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), "fade"));

        Assert.Equal(100, result.Length);
    }

    [Fact]
    public async Task ApplyFade_NoFade_ChangesNothing()
    {
        short[] original = WavTestFiles.Ramp(200);

        short[] result = await SamplesOf(await _service.ApplyFadeAsync(await Source(original), TimeSpan.Zero, TimeSpan.Zero, "fade"));

        Assert.Equal(original, result);
    }

    [Fact]
    public async Task ApplyFade_RampsInFromSilenceAndOutToSilenceLeavingTheMiddleAlone()
    {
        string source = await Source(WavTestFiles.Constant(10000, 1000));

        short[] result = await SamplesOf(await _service.ApplyFadeAsync(source, TimeSpan.FromSeconds(0.25), TimeSpan.FromSeconds(0.5), "fade"));

        Assert.Equal(0, result[0]);
        Assert.Equal(5000, result[125]); // halfway through the 250-sample fade-in
        Assert.Equal(10000, result[250]);
        Assert.Equal(10000, result[499]);
        Assert.Equal(5000, result[^251]); // halfway through the 500-sample fade-out
        Assert.Equal(0, result[^1]);
    }

    [Fact]
    public async Task ApplyFade_StereoFadeLastsTheSameTimeAsMono()
    {
        string source = await Source(WavTestFiles.Constant(10000, 2000), channels: 2); // one second

        short[] result = await SamplesOf(await _service.ApplyFadeAsync(source, TimeSpan.FromSeconds(0.25), TimeSpan.Zero, "fade"));

        Assert.Equal(5000, result[250]); // 0.125 s in: half of a 0.25 s fade, at 2 samples per frame
        Assert.Equal(10000, result[500]);
    }

    [Fact]
    public async Task ApplyGain_NegativeSixDecibelsHalves()
    {
        string source = await Source([1000, -2000, 3000]);

        short[] result = await SamplesOf(await _service.ApplyGainAsync(source, -20 * Math.Log10(2), "quieter"));

        Assert.Equal([500, -1000, 1500], result);
    }

    [Fact]
    public async Task ApplyGain_SixDecibelsRoughlyDoublesAndClipsAtFullScale()
    {
        string source = await Source([1000, -2000, 20000, -20000]);

        short[] result = await SamplesOf(await _service.ApplyGainAsync(source, 20 * Math.Log10(2), "louder"));

        Assert.Equal([2000, -4000, short.MaxValue, short.MinValue], result);
    }

    [Fact]
    public async Task ApplyGain_ZeroDecibelsChangesNothing()
    {
        short[] original = WavTestFiles.Ramp(50);

        short[] result = await SamplesOf(await _service.ApplyGainAsync(await Source(original), 0, "same"));

        Assert.Equal(original, result);
    }

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(12, 500)]
    [InlineData(-12, 2000)]
    public async Task ApplyVoiceChange_AnOctaveHalvesOrDoublesTheLengthLikeATurntable(double semitones, int expectedLength)
    {
        string source = await Source(WavTestFiles.Ramp(1000));

        WavFile result = await WavTestFiles.ReadAsync(await _service.ApplyVoiceChangeAsync(source, semitones, "pitch"));

        Assert.Equal(expectedLength, result.Samples.Length);
        Assert.Equal(1000, result.SampleRate);
    }

    [Fact]
    public async Task ApplyVoiceChange_NoShift_KeepsTheSamples()
    {
        short[] original = WavTestFiles.Ramp(200);

        short[] result = await SamplesOf(await _service.ApplyVoiceChangeAsync(await Source(original), 0, "pitch"));

        Assert.Equal(original, result);
    }

    [Fact]
    public async Task Cut_EmptySelection_RemovesNothing()
    {
        short[] original = WavTestFiles.Ramp(100);

        short[] result = await SamplesOf(await _service.CutAsync(await Source(original), TimeSpan.FromSeconds(0.05), TimeSpan.FromSeconds(0.05), "cut"));

        Assert.Equal(original, result);
    }

    [Fact]
    public async Task Cut_EndBeforeStart_RemovesNothing()
    {
        short[] original = WavTestFiles.Ramp(100);

        short[] result = await SamplesOf(await _service.CutAsync(await Source(original), TimeSpan.FromSeconds(0.08), TimeSpan.FromSeconds(0.02), "cut"));

        Assert.Equal(original, result);
    }

    [Fact]
    public async Task Cut_RemovesTheSelectedRangeAndJoinsWhatIsLeft()
    {
        string source = await Source(WavTestFiles.Ramp(1000));

        short[] result = await SamplesOf(await _service.CutAsync(source, TimeSpan.FromSeconds(0.2), TimeSpan.FromSeconds(0.5), "cut"));

        Assert.Equal(700, result.Length);
        Assert.Equal(200, result[199]);
        Assert.Equal(501, result[200]);
        Assert.Equal(1000, result[^1]);
    }

    [Fact]
    public async Task Cut_Stereo_BetweenFrames_KeepsLeftAndRightTheRightWayRound()
    {
        string source = await Source(WavTestFiles.Ramp(2000), channels: 2);

        // Three samples in to four samples in: removes a single sample, which would leave everything after it a channel out.
        short[] result = await SamplesOf(await _service.CutAsync(source, TimeSpan.FromSeconds(0.0015), TimeSpan.FromSeconds(0.002), "cut"));

        for (int i = 0; i < result.Length; i += 2)
        {
            Assert.True(result[i] % 2 == 1, $"sample {i} is {result[i]}, a right-channel sample where a left one belongs");
        }
    }

    public void Dispose() => _files.Dispose();

    [Fact]
    public async Task Edits_BlankSourceOrOutputName_IsRejectedUpFront()
    {
        string source = await Source(WavTestFiles.Ramp(10));

        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.ApplyGainAsync(string.Empty, 0, "x"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.ApplyGainAsync(source, 0, " "));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.TrimAsync(source, TimeSpan.Zero, TimeSpan.FromSeconds(1), string.Empty));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.InsertAsync(source, string.Empty, TimeSpan.Zero, "x"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.SplitAsync(source, TimeSpan.Zero, "a", " "));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.GetWaveformPeaksAsync(string.Empty, 10));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.FindNearestZeroCrossingAsync(string.Empty, TimeSpan.Zero, TimeSpan.Zero));
    }

    [Fact]
    public async Task Edits_KeepTheSourcesChannelsAndSampleRate()
    {
        string source = await Source(WavTestFiles.Ramp(40), channels: 2, sampleRate: 8000);

        WavFile result = await WavTestFiles.ReadAsync(await _service.ApplyGainAsync(source, 0, "same"));

        Assert.Equal(2, result.Channels);
        Assert.Equal(8000, result.SampleRate);
        Assert.Equal(16, result.BitsPerSample);
    }

    [Fact]
    public async Task Edits_MissingSourceFile_ThrowsFileNotFound() { await Assert.ThrowsAsync<FileNotFoundException>(() => _service.ReverseAsync(_files.InAppData("nope.wav"), "x")); }
    [Fact]
    public async Task Edits_OutputNameWithForbiddenCharacters_StaysInsideTheExportsFolder()
    {
        string source = await Source(WavTestFiles.Ramp(10));

        string output = await _service.NormalizeAsync(source, "a/b\\c:d");

        Assert.Equal(_files.InAppData("Exports"), Path.GetDirectoryName(output));
    }

    [Fact]
    public async Task Edits_WriteANewWavInTheExportsFolderAndLeaveTheSourceAlone()
    {
        short[] original = WavTestFiles.Ramp(100);
        string source = await Source(original);

        string output = await _service.ReverseAsync(source, "backwards");

        Assert.Equal(_files.InAppData("Exports"), Path.GetDirectoryName(output));
        Assert.StartsWith("backwards_", Path.GetFileName(output));
        Assert.EndsWith(".wav", output);
        Assert.Equal(original, await SamplesOf(source));
    }

    [Fact]
    public async Task FindNearestZeroCrossing_ClipTooShortToHaveOne_ReturnsThePositionUnchanged()
    {
        string source = await Source([5]);

        TimeSpan result = await _service.FindNearestZeroCrossingAsync(source, TimeSpan.FromSeconds(0.7), TimeSpan.FromSeconds(0.05));

        Assert.Equal(TimeSpan.FromSeconds(0.7), result);
    }

    [Fact]
    public async Task FindNearestZeroCrossing_CrossingBeyondTheSearchWindow_IsIgnored()
    {
        short[] samples = [.. WavTestFiles.Constant(100, 500), .. WavTestFiles.Constant(-100, 500)];
        string source = await Source(samples);

        TimeSpan result = await _service.FindNearestZeroCrossingAsync(source, TimeSpan.FromSeconds(0.2), TimeSpan.FromSeconds(0.05));

        Assert.Equal(0.2, result.TotalSeconds, 6);
    }

    [Fact]
    public async Task FindNearestZeroCrossing_NoneWithinReach_LeavesThePositionWhereItWas()
    {
        string source = await Source(WavTestFiles.Constant(100, 1000));

        TimeSpan result = await _service.FindNearestZeroCrossingAsync(source, TimeSpan.FromSeconds(0.4), TimeSpan.FromSeconds(0.05));

        Assert.Equal(0.4, result.TotalSeconds, 6);
    }

    [Fact]
    public async Task FindNearestZeroCrossing_SnapsToTheClosestSignChange()
    {
        // Positive for the first 300 samples, negative for the next 300, then positive again: crossings after 299 and 599.
        short[] samples = [.. WavTestFiles.Constant(100, 300), .. WavTestFiles.Constant(-100, 300), .. WavTestFiles.Constant(100, 400)];
        string source = await Source(samples);

        TimeSpan nearFirst = await _service.FindNearestZeroCrossingAsync(source, TimeSpan.FromSeconds(0.31), TimeSpan.FromSeconds(0.05));
        TimeSpan nearSecond = await _service.FindNearestZeroCrossingAsync(source, TimeSpan.FromSeconds(0.58), TimeSpan.FromSeconds(0.05));

        Assert.Equal(0.299, nearFirst.TotalSeconds, 6);
        Assert.Equal(0.599, nearSecond.TotalSeconds, 6);
    }

    [Fact]
    public async Task GetWaveformPeaks_EmptyClip_GivesNone() { Assert.Empty(await _service.GetWaveformPeaksAsync(await Source([]), 10)); }
    [Fact]
    public async Task GetWaveformPeaks_FullScaleNegativeSample_IsNeverNegativeOrAboveOne()
    {
        string source = await Source([short.MinValue, 0, 0, 0]);

        float[] peaks = await _service.GetWaveformPeaksAsync(source, 2);

        Assert.All(peaks, peak => Assert.InRange(peak, 0f, 1f));
        Assert.Equal(1f, peaks[0]);
    }

    [Fact]
    public async Task GetWaveformPeaks_GivesTheLoudestSampleOfEachStretchAsAFraction()
    {
        // Ten peaks over 1000 samples: 100 samples each; the loudest of each block is what should come back.
        short[] samples = WavTestFiles.Constant(0, 1000);
        samples[50] = 16384;
        samples[250] = -32767;
        samples[999] = 8192;
        string source = await Source(samples);

        float[] peaks = await _service.GetWaveformPeaksAsync(source, 10);

        Assert.Equal(10, peaks.Length);
        Assert.Equal(16384 / (float)short.MaxValue, peaks[0], 5);
        Assert.Equal(0, peaks[1]);
        Assert.Equal(1f, peaks[2], 5);
        Assert.Equal(8192 / (float)short.MaxValue, peaks[9], 5);
    }

    [Fact]
    public async Task GetWaveformPeaks_MorePeaksThanSamples_PadsTheEndWithSilence()
    {
        float[] peaks = await _service.GetWaveformPeaksAsync(await Source([short.MaxValue, short.MaxValue]), 5);

        Assert.Equal([1f, 1f, 0f, 0f, 0f], peaks);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task GetWaveformPeaks_NoPeaksWanted_GivesNone(int peakCount) { Assert.Empty(await _service.GetWaveformPeaksAsync(await Source(WavTestFiles.Ramp(100)), peakCount)); }
    [Fact]
    public async Task Insert_ClipInAnotherFormat_IsConvertedToTheSourcesFormatFirst()
    {
        string source = await Source(WavTestFiles.Constant(100, 1000), channels: 1, sampleRate: 1000);
        // A stereo clip at twice the rate: 200 frames = 0.1 s, so it should become 100 mono frames.
        string other = await Source(WavTestFiles.Constant(7, 400), channels: 2, sampleRate: 2000, name: "other.wav");

        WavFile result = await WavTestFiles.ReadAsync(await _service.InsertAsync(source, other, TimeSpan.Zero, "insert"));

        Assert.Equal(1, result.Channels);
        Assert.Equal(1000, result.SampleRate);
        Assert.Equal(1100, result.Samples.Length);
        Assert.All(result.Samples[..100], sample => Assert.Equal(7, sample));
    }

    [Theory]
    [InlineData(-4, 0)]
    [InlineData(99, 1000)]
    public async Task Insert_PositionPastTheEnds_GoesAtTheNearestEnd(double seconds, int expectedIndex)
    {
        string source = await Source(WavTestFiles.Ramp(1000));
        string other = await Source(WavTestFiles.Constant(-1, 10), name: "other.wav");

        short[] result = await SamplesOf(await _service.InsertAsync(source, other, TimeSpan.FromSeconds(seconds), "insert"));

        Assert.All(result[expectedIndex..(expectedIndex + 10)], sample => Assert.Equal(-1, sample));
    }

    [Fact]
    public async Task Insert_PutsTheOtherClipInAtThatPosition()
    {
        string source = await Source(WavTestFiles.Ramp(1000));
        string other = await Source(WavTestFiles.Constant(-1, 100), name: "other.wav");

        short[] result = await SamplesOf(await _service.InsertAsync(source, other, TimeSpan.FromSeconds(0.5), "insert"));

        Assert.Equal(1100, result.Length);
        Assert.Equal(500, result[499]);
        Assert.All(result[500..600], sample => Assert.Equal(-1, sample));
        Assert.Equal(501, result[600]);
    }

    [Fact]
    public async Task Insert_Stereo_BetweenFrames_DoesNotSwapTheChannelsOfWhatFollows()
    {
        string source = await Source(WavTestFiles.Ramp(2000), channels: 2);
        string other = await Source(WavTestFiles.Constant(-2, 10), channels: 2, name: "other.wav");

        short[] result = await SamplesOf(await _service.InsertAsync(source, other, TimeSpan.FromSeconds(0.0015), "insert"));

        int insertedAt = Array.IndexOf(result, (short)-2);
        Assert.True(insertedAt % 2 == 0, $"the insert starts at sample {insertedAt}, part-way through a frame");
        short[] tail = result[(insertedAt + 10)..];
        Assert.True(StartsOnALeftSample(tail), $"what follows the insert starts on sample {tail[0]}");
    }

    [Fact]
    public async Task Normalize_ClipContainingAFullScaleNegativeSample_ScalesToItInsteadOfIgnoringIt()
    {
        // -32768 is the loudest a 16-bit sample gets, but its absolute value doesn't fit in a short.
        string source = await Source([short.MinValue, 10000, -10000]);

        short[] result = await SamplesOf(await _service.NormalizeAsync(source, "norm"));

        Assert.Equal(-short.MaxValue, result[0]); // scaled to the 32767 ceiling, like every other clip
        Assert.InRange(result[1], 9999, 10000); // the clip was already at full scale, so (almost) untouched rather than boosted 3x
    }

    [Fact]
    public async Task Normalize_EmptyFile_WritesAnEmptyFile()
    {
        short[] result = await SamplesOf(await _service.NormalizeAsync(await Source([]), "norm"));

        Assert.Empty(result);
    }

    [Fact]
    public async Task Normalize_ScalesTheLoudestSampleToFullScale()
    {
        string source = await Source([1000, -4000, 2000]);

        short[] result = await SamplesOf(await _service.NormalizeAsync(source, "norm"));

        Assert.Equal(short.MaxValue, result.Max(sample => Math.Abs((int)sample)));
        Assert.Equal(-short.MaxValue, result[1]);
        Assert.Equal(Math.Round(1000 * (short.MaxValue / 4000.0)), result[0]);
    }

    [Fact]
    public async Task Normalize_Silence_StaysSilent()
    {
        short[] result = await SamplesOf(await _service.NormalizeAsync(await Source(WavTestFiles.Constant(0, 20)), "norm"));

        Assert.All(result, sample => Assert.Equal(0, sample));
    }

    [Fact]
    public async Task ReduceNoise_KeepsTheLengthAndFormat()
    {
        Random random = new(7);
        short[] noise = [.. Enumerable.Range(0, Rate).Select(_ => (short)random.Next(-500, 500))];
        string source = await Source(noise, sampleRate: Rate);

        WavFile result = await WavTestFiles.ReadAsync(await _service.ReduceNoiseAsync(source, TimeSpan.FromSeconds(0.25), "denoise"));

        Assert.Equal(noise.Length, result.Samples.Length);
        Assert.Equal(Rate, result.SampleRate);
    }

    [Fact]
    public async Task RemoveVocals_KeepsOnlyWhatDiffersBetweenTheChannels()
    {
        // Frames (L,R): identical (centre-panned, like a vocal) then different.
        string source = await Source([1000, 1000, -500, -500, 1000, 200, 0, 300], channels: 2);

        short[] result = await SamplesOf(await _service.RemoveVocalsAsync(source, "karaoke"));

        Assert.Equal([0, 0, 0, 0, 800, 800, -300, -300], result);
    }

    [Fact]
    public async Task RemoveVocals_Mono_IsRefused()
    {
        string source = await Source(WavTestFiles.Ramp(10));

        await Assert.ThrowsAsync<NotSupportedException>(() => _service.RemoveVocalsAsync(source, "karaoke"));
    }

    [Fact]
    public async Task Reverse_Mono_PlaysBackwards()
    {
        short[] result = await SamplesOf(await _service.ReverseAsync(await Source([1, 2, 3, 4, 5]), "rev"));

        Assert.Equal([5, 4, 3, 2, 1], result);
    }

    [Fact]
    public async Task Reverse_Stereo_ReversesFramesWithoutSwappingChannels()
    {
        // Frames (L,R): (1,2) (3,4) (5,6)
        short[] result = await SamplesOf(await _service.ReverseAsync(await Source([1, 2, 3, 4, 5, 6], channels: 2), "rev"));

        Assert.Equal([5, 6, 3, 4, 1, 2], result);
    }

    [Fact]
    public async Task Reverse_ThenReverse_GivesTheOriginalBack()
    {
        short[] original = WavTestFiles.Ramp(101);
        string once = await _service.ReverseAsync(await Source(original), "rev");

        short[] twice = await SamplesOf(await _service.ReverseAsync(once, "rev"));

        Assert.Equal(original, twice);
    }

    [Fact]
    public async Task Split_MakesTwoFilesThatTogetherAreTheOriginal()
    {
        short[] original = WavTestFiles.Ramp(1000);
        string source = await Source(original);

        (string beforePath, string afterPath) = await _service.SplitAsync(source, TimeSpan.FromSeconds(0.4), "one", "two");

        short[] before = await SamplesOf(beforePath);
        short[] after = await SamplesOf(afterPath);
        Assert.Equal(400, before.Length);
        Assert.Equal(600, after.Length);
        short[] joined = [.. before, .. after];
        Assert.Equal(original, joined);
        Assert.StartsWith("one_", Path.GetFileName(beforePath));
        Assert.StartsWith("two_", Path.GetFileName(afterPath));
    }

    [Theory]
    [InlineData(-3, 0, 100)]
    [InlineData(0, 0, 100)]
    [InlineData(99, 100, 0)]
    public async Task Split_PositionAtOrPastTheEnds_GivesAnEmptyHalf(double seconds, int expectedBefore, int expectedAfter)
    {
        string source = await Source(WavTestFiles.Ramp(100));

        (string beforePath, string afterPath) = await _service.SplitAsync(source, TimeSpan.FromSeconds(seconds), "one", "two");

        Assert.Equal(expectedBefore, (await SamplesOf(beforePath)).Length);
        Assert.Equal(expectedAfter, (await SamplesOf(afterPath)).Length);
    }

    [Fact]
    public async Task Split_Stereo_BetweenFrames_BothHalvesStartOnALeftSample()
    {
        string source = await Source(WavTestFiles.Ramp(2000), channels: 2);

        (string beforePath, string afterPath) = await _service.SplitAsync(source, TimeSpan.FromSeconds(0.0015), "one", "two");

        short[] before = await SamplesOf(beforePath);
        short[] after = await SamplesOf(afterPath);
        Assert.True(StartsOnALeftSample(after), $"the second half starts on sample {after[0]}");
        Assert.Equal(0, before.Length % 2);
        Assert.Equal(2000, before.Length + after.Length);
    }

    [Fact]
    public async Task Trim_EndBeforeStart_GivesAnEmptyButValidFile()
    {
        string source = await Source(WavTestFiles.Ramp(100));

        WavFile result = await WavTestFiles.ReadAsync(await _service.TrimAsync(source, TimeSpan.FromSeconds(0.08), TimeSpan.FromSeconds(0.02), "trim"));

        Assert.Empty(result.Samples);
        Assert.Equal(TimeSpan.Zero, result.Duration);
    }

    [Fact]
    public async Task Trim_KeepsOnlyTheSelectedRange()
    {
        string source = await Source(WavTestFiles.Ramp(1000));

        short[] result = await SamplesOf(await _service.TrimAsync(source, TimeSpan.FromSeconds(0.2), TimeSpan.FromSeconds(0.5), "trim"));

        Assert.Equal(300, result.Length);
        Assert.Equal(201, result[0]);
        Assert.Equal(500, result[^1]);
    }

    [Fact]
    public async Task Trim_RangePastTheEnds_IsClampedToTheClip()
    {
        short[] original = WavTestFiles.Ramp(100);
        string source = await Source(original);

        short[] result = await SamplesOf(await _service.TrimAsync(source, TimeSpan.FromSeconds(-5), TimeSpan.FromSeconds(99), "trim"));

        Assert.Equal(original, result);
    }

    [Theory]
    [InlineData(0.0015)]
    [InlineData(0.0035)]
    public async Task Trim_Stereo_EndingBetweenFrames_DoesNotEndOnAHalfFrame(double endSeconds)
    {
        string source = await Source(WavTestFiles.Ramp(2000), channels: 2);

        short[] result = await SamplesOf(await _service.TrimAsync(source, TimeSpan.Zero, TimeSpan.FromSeconds(endSeconds), "trim"));

        Assert.Equal(0, result.Length % 2);
    }

    [Fact]
    public async Task Trim_Stereo_OnAFrameBoundary_IsExact()
    {
        string source = await Source(WavTestFiles.Ramp(2000), channels: 2);

        short[] result = await SamplesOf(await _service.TrimAsync(source, TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(0.2), "trim"));

        Assert.Equal(201, result[0]); // 0.1 s = 100 frames = 200 samples in, so the first kept sample is number 201
        Assert.Equal(200, result.Length); // 0.1 s of stereo is 100 frames = 200 samples
    }

    [Theory]
    [InlineData(0.0015)] // 3.0 samples in: between a right and a left sample
    [InlineData(0.0035)]
    [InlineData(0.00125)]
    public async Task Trim_Stereo_StartingBetweenFrames_KeepsLeftAndRightTheRightWayRound(double startSeconds)
    {
        string source = await Source(WavTestFiles.Ramp(2000), channels: 2);

        short[] result = await SamplesOf(await _service.TrimAsync(source, TimeSpan.FromSeconds(startSeconds), TimeSpan.FromSeconds(0.5), "trim"));

        Assert.True(StartsOnALeftSample(result), $"the trimmed clip starts on sample {result[0]}, a right-channel sample");
        Assert.Equal(0, result.Length % 2);
    }
    #endregion
}
