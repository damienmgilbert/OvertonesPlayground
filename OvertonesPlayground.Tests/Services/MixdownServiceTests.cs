using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Tests.Services;

public sealed class MixdownServiceTests : IDisposable
{
    private const int Rate = 44_100;

    private readonly TempFileSystem _files = new();
    private readonly MixdownService _service;
    private int _clipNumber;

    public MixdownServiceTests() => _service = new MixdownService(_files);

    public void Dispose() => _files.Dispose();

    /// <summary>
    /// A track holding one mono clip of <paramref name="samples"/> at the mix's own sample rate, placed <paramref name="offsetFrames"/> in.
    /// </summary>
    private async Task<Track> TrackWithClipAsync(short[] samples, double volume = 1, double pan = 0, bool muted = false, bool soloed = false, double gainDb = 0, int offsetFrames = 0, int channels = 1, int sampleRate = Rate)
    {
        string path = await WavTestFiles.WriteAsync(_files.InAppData("clips", $"clip{_clipNumber++}.wav"), samples, channels, sampleRate);
        Track track = new() { Name = "Track", Volume = volume, Pan = pan, IsMuted = muted, IsSoloed = soloed };
        track.Clips.Add(new TrackClip { ClipFilePath = path, ClipName = "Clip", GainDb = gainDb, StartOffset = TimeSpan.FromSeconds((double)offsetFrames / Rate) });
        return track;
    }

    private async Task<WavFile> RenderAsync(params Track[] tracks)
    {
        string path = await _service.RenderAsync(new MixProject { Name = "Mix", Tracks = [.. tracks] }, "mix");
        return await WavTestFiles.ReadAsync(path);
    }

    #region Output
    [Fact]
    public async Task Render_WritesAStereo44kFileInTheMixesFolder()
    {
        Track track = await TrackWithClipAsync([1, 2, 3]);

        string path = await _service.RenderAsync(new MixProject { Name = "Mix", Tracks = [track] }, "my mix");
        WavFile result = await WavTestFiles.ReadAsync(path);

        Assert.Equal(_files.InAppData("Mixes"), Path.GetDirectoryName(path));
        Assert.StartsWith("my mix_", Path.GetFileName(path));
        Assert.Equal(2, result.Channels);
        Assert.Equal(Rate, result.SampleRate);
        Assert.Equal(16, result.BitsPerSample);
    }

    [Fact]
    public async Task Render_MonoClipCentredAtFullVolume_PlaysEqualInBothChannels()
    {
        WavFile result = await RenderAsync(await TrackWithClipAsync([1000, -2000]));

        Assert.Equal([1000, 1000, -2000, -2000], result.Samples);
    }

    [Fact]
    public async Task Render_StereoClipAtTheMixRate_IsUsedAsItIs()
    {
        WavFile result = await RenderAsync(await TrackWithClipAsync([100, 200, 300, 400], channels: 2));

        Assert.Equal([100, 200, 300, 400], result.Samples);
    }

    [Fact]
    public async Task Render_ClipAtAnotherSampleRate_IsResampledToTheMixRate()
    {
        // 100 mono frames at half the mix rate become 200 frames, i.e. 400 stereo samples.
        WavFile result = await RenderAsync(await TrackWithClipAsync(WavTestFiles.Constant(500, 100), sampleRate: Rate / 2));

        Assert.Equal(400, result.Samples.Length);
        Assert.Equal(Rate, result.SampleRate);
    }
    #endregion

    #region Level and position
    [Fact]
    public async Task Render_TrackVolume_ScalesBothChannels()
    {
        WavFile result = await RenderAsync(await TrackWithClipAsync([1000, -2000], volume: 0.5));

        Assert.Equal([500, 500, -1000, -1000], result.Samples);
    }

    [Theory]
    [InlineData(0.5, 500, 1000)] // panned right: the left channel is turned down
    [InlineData(1.0, 0, 1000)]
    [InlineData(-0.5, 1000, 500)] // panned left: the right channel is turned down
    [InlineData(-1.0, 1000, 0)]
    [InlineData(0.0, 1000, 1000)]
    public async Task Render_Pan_TurnsDownTheChannelOnTheOppositeSide(double pan, short expectedLeft, short expectedRight)
    {
        WavFile result = await RenderAsync(await TrackWithClipAsync([1000], pan: pan));

        Assert.Equal([expectedLeft, expectedRight], result.Samples);
    }

    [Fact]
    public async Task Render_PanAndVolumeTogether_MultiplyEachOther()
    {
        WavFile result = await RenderAsync(await TrackWithClipAsync([1000], volume: 0.5, pan: 0.5));

        Assert.Equal([250, 500], result.Samples);
    }

    [Fact]
    public async Task Render_ClipGain_IsAppliedInDecibels()
    {
        WavFile result = await RenderAsync(await TrackWithClipAsync([1000, -2000], gainDb: -20 * Math.Log10(2)));

        Assert.Equal([500, 500, -1000, -1000], result.Samples);
    }

    [Fact]
    public async Task Render_ClipGainThatClips_IsHeldAtFullScale()
    {
        WavFile result = await RenderAsync(await TrackWithClipAsync([20000, -20000], gainDb: 20 * Math.Log10(2)));

        Assert.Equal([short.MaxValue, short.MaxValue, short.MinValue, short.MinValue], result.Samples);
    }

    [Fact]
    public async Task Render_ClipOffset_LeavesSilenceBeforeIt()
    {
        WavFile result = await RenderAsync(await TrackWithClipAsync([7, 8], offsetFrames: 3));

        Assert.Equal([0, 0, 0, 0, 0, 0, 7, 7, 8, 8], result.Samples);
    }

    [Fact]
    public async Task Render_LengthIsThatOfTheLatestEndingClip()
    {
        Track early = await TrackWithClipAsync(WavTestFiles.Constant(1, 10));
        Track late = await TrackWithClipAsync(WavTestFiles.Constant(1, 5), offsetFrames: 20);

        WavFile result = await RenderAsync(early, late);

        Assert.Equal((20 + 5) * 2, result.Samples.Length);
    }

    [Fact]
    public async Task Render_SeveralClipsOnOneTrack_AreAllMixedInAtTheirOwnPositions()
    {
        Track track = await TrackWithClipAsync([1, 1]);
        string second = await WavTestFiles.WriteAsync(_files.InAppData("clips", "second.wav"), [5, 5], 1, Rate);
        track.Clips.Add(new TrackClip { ClipFilePath = second, StartOffset = TimeSpan.FromSeconds(4.0 / Rate) });

        WavFile result = await RenderAsync(track);

        Assert.Equal([1, 1, 1, 1, 0, 0, 0, 0, 5, 5, 5, 5], result.Samples);
    }
    #endregion

    #region Mixing tracks together
    [Fact]
    public async Task Render_TwoTracks_AreAddedTogether()
    {
        WavFile result = await RenderAsync(await TrackWithClipAsync([1000, 2000]), await TrackWithClipAsync([10, -20]));

        Assert.Equal([1010, 1010, 1980, 1980], result.Samples);
    }

    [Fact]
    public async Task Render_TracksThatAddUpTooLoud_ClipAtFullScaleRatherThanWrapping()
    {
        WavFile result = await RenderAsync(await TrackWithClipAsync([30000, -30000]), await TrackWithClipAsync([30000, -30000]));

        Assert.Equal([short.MaxValue, short.MaxValue, short.MinValue, short.MinValue], result.Samples);
    }

    [Fact]
    public async Task Render_MutedTrack_IsLeftOut()
    {
        WavFile result = await RenderAsync(await TrackWithClipAsync([1000]), await TrackWithClipAsync([500], muted: true));

        Assert.Equal([1000, 1000], result.Samples);
    }

    [Fact]
    public async Task Render_WhenAnyTrackIsSoloed_OnlySoloedTracksAreHeard()
    {
        WavFile result = await RenderAsync(await TrackWithClipAsync([1000]), await TrackWithClipAsync([200], soloed: true), await TrackWithClipAsync([30]));

        Assert.Equal([200, 200], result.Samples);
    }

    [Fact]
    public async Task Render_ASoloedTrackIsHeardEvenIfItIsAlsoMuted()
    {
        WavFile result = await RenderAsync(await TrackWithClipAsync([1000]), await TrackWithClipAsync([200], soloed: true, muted: true));

        Assert.Equal([200, 200], result.Samples);
    }
    #endregion

    #region Refusing
    [Fact]
    public async Task Render_NothingAudible_ThrowsInsteadOfWritingAnEmptyFile()
    {
        Track muted = await TrackWithClipAsync([1000], muted: true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RenderAsync(new MixProject { Name = "Mix", Tracks = [muted] }, "mix"));
        Assert.False(Directory.Exists(_files.InAppData("Mixes")) && Directory.GetFiles(_files.InAppData("Mixes")).Length > 0);
    }

    [Fact]
    public async Task Render_TracksWithNoClips_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RenderAsync(new MixProject { Name = "Mix", Tracks = [new Track(), new Track()] }, "mix"));
    }

    [Fact]
    public async Task Render_NoProject_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.RenderAsync(null!, "mix"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Render_BlankOutputName_Throws(string name)
    {
        Track track = await TrackWithClipAsync([1]);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => _service.RenderAsync(new MixProject { Tracks = [track] }, name));
    }

    [Fact]
    public async Task Render_ClipFileMissing_ThrowsFileNotFound()
    {
        Track track = new();
        track.Clips.Add(new TrackClip { ClipFilePath = _files.InAppData("gone.wav") });

        await Assert.ThrowsAsync<FileNotFoundException>(() => _service.RenderAsync(new MixProject { Tracks = [track] }, "mix"));
    }
    #endregion
}
