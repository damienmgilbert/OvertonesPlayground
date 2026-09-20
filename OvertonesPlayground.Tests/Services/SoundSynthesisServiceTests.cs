using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Tests.Services;

public sealed class SoundSynthesisServiceTests : IDisposable
{
    #region Fields
    private readonly TempFileSystem _files = new();
    private readonly SoundSynthesisService _service;
    #endregion

    #region Constructors
    public SoundSynthesisServiceTests() { _service = new SoundSynthesisService(_files); }
    #endregion

    #region Public methods
    public static TheoryData<DrumType> AllDrums() => [.. Enum.GetValues<DrumType>()];

    public void Dispose() => _files.Dispose();

    [Theory]
    [MemberData(nameof(AllDrums))]
    public async Task GenerateDrum_EveryDrumProducesAPlayableFileOfItsLength(DrumType drum)
    {
        DrumSynthParameters parameters = DrumSynthParameters.CreateDefault(drum);

        AudioClip clip = await _service.GenerateDrumAsync(drum, drum.ToString(), parameters);

        WavFile wav = await WavTestFiles.ReadAsync(clip.FilePath);
        Assert.Equal(drum.ToString(), clip.Name);
        Assert.True(clip.IsUserRecording);
        Assert.StartsWith($"drum_{drum}_", Path.GetFileName(clip.FilePath));
        Assert.Equal((int)(parameters.DurationSeconds * parameters.SampleRate), wav.Samples.Length);
        Assert.Contains(wav.Samples, sample => sample != 0);
    }

    [Fact]
    public async Task GenerateDrum_NotADrum_Throws() { await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _service.GenerateDrumAsync((DrumType)999, "?", new DrumSynthParameters())); }
    [Fact]
    public async Task GenerateDrum_UsesTheParametersSampleRate()
    {
        DrumSynthParameters parameters = DrumSynthParameters.CreateDefault(DrumType.Kick);
        parameters.SampleRate = 22_050;

        AudioClip clip = await _service.GenerateDrumAsync(DrumType.Kick, "Kick", parameters);

        WavFile wav = await WavTestFiles.ReadAsync(clip.FilePath);
        Assert.Equal(22_050, wav.SampleRate);
        Assert.Equal((int)(parameters.DurationSeconds * 22_050), wav.Samples.Length);
    }

    [Theory]
    [InlineData(WaveformType.Square, "tone_Square_")]
    [InlineData(WaveformType.PinkNoise, "tone_PinkNoise_")]
    public async Task GenerateTone_FileNameSaysWhichWaveformItIs(WaveformType waveform, string prefix)
    {
        AudioClip clip = await _service.GenerateToneAsync(waveform, 300, 0.1, 0.5, "x");

        Assert.StartsWith(prefix, Path.GetFileName(clip.FilePath));
    }

    [Fact]
    public async Task GenerateTone_LevelFollowsTheRequestedAmplitude()
    {
        AudioClip clip = await _service.GenerateToneAsync(WaveformType.Sine, 440, 1, 0.5, "half");

        short[] samples = (await WavTestFiles.ReadAsync(clip.FilePath)).Samples;

        Assert.InRange(samples.Max(), 0.49 * short.MaxValue, 0.5 * short.MaxValue);
        Assert.InRange(samples.Min(), -0.5 * short.MaxValue, -0.49 * short.MaxValue);
    }

    [Fact]
    public async Task GenerateTone_ManyTonesInARow_EachGetsItsOwnFileWithItsOwnSound()
    {
        // Generated as fast as they can be, so several land in the same millisecond and second: none may overwrite another.
        List<AudioClip> clips = [];
        for (int i = 1; i <= 8; i++)
        {
            clips.Add(await _service.GenerateToneAsync(WaveformType.Sine, 100 * i, 0.02, 0.5, $"tone {i}"));
        }

        Assert.Equal(8, clips.Select(clip => clip.FilePath).Distinct().Count());
        Assert.All(clips, clip => Assert.True(File.Exists(clip.FilePath)));

        // Each file still holds its own tone: none was overwritten by a later one.
        HashSet<string> sounds = [];
        foreach (AudioClip clip in clips)
        {
            short[] samples = (await WavTestFiles.ReadAsync(clip.FilePath)).Samples;
            _ = sounds.Add(string.Join(',', samples));
        }

        Assert.Equal(8, sounds.Count);
    }

    [Fact]
    public async Task GenerateTone_WritesAMonoWavAndReturnsAClipForIt()
    {
        AudioClip clip = await _service.GenerateToneAsync(WaveformType.Sine, 440, 0.5, 0.5, "A440");

        Assert.Equal("A440", clip.Name);
        Assert.True(clip.IsUserRecording);
        Assert.Equal(_files.InAppData("Synth"), Path.GetDirectoryName(clip.FilePath));
        Assert.StartsWith("tone_Sine_", Path.GetFileName(clip.FilePath));

        WavFile wav = await WavTestFiles.ReadAsync(clip.FilePath);
        Assert.Equal(1, wav.Channels);
        Assert.Equal(44_100, wav.SampleRate);
        Assert.Equal(22_050, wav.Samples.Length);
        Assert.Equal(wav.Duration, clip.Duration);
        Assert.Equal(0.5, clip.Duration.TotalSeconds, 3);
    }
    #endregion
}
