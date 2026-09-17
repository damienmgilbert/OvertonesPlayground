using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.Services.Implementations;

public class SoundSynthesisService : ISoundSynthesisService
{
    private const int SampleRate = 44_100;

    private readonly IAudioLibraryService _libraryService;

    public SoundSynthesisService(IAudioLibraryService libraryService)
    {
        _libraryService = libraryService;
    }

    private static string SynthDirectory
    {
        get
        {
            var dir = Path.Combine(FileSystem.AppDataDirectory, "Synth");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public async Task<AudioClip> GenerateToneAsync(
        WaveformType waveform, double frequencyHz, double durationSeconds, double amplitude, string name)
    {
        var samples = WaveformGenerator.Generate(waveform, frequencyHz, durationSeconds, SampleRate, amplitude);
        var path = await WriteAsync(samples, $"tone_{waveform}");
        return await _libraryService.AddClipAsync(path, name, isUserRecording: true);
    }

    public async Task<AudioClip> GenerateDrumAsync(DrumType drum, string name)
    {
        var samples = drum switch
        {
            DrumType.Kick => DrumSynthesizer.Kick(SampleRate),
            DrumType.Snare => DrumSynthesizer.Snare(SampleRate),
            DrumType.HiHat => DrumSynthesizer.HiHat(SampleRate),
            DrumType.Clap => DrumSynthesizer.Clap(SampleRate),
            DrumType.Bass => DrumSynthesizer.Bass(SampleRate),
            _ => throw new ArgumentOutOfRangeException(nameof(drum)),
        };

        var path = await WriteAsync(samples, $"drum_{drum}");
        return await _libraryService.AddClipAsync(path, name, isUserRecording: true);
    }

    private static async Task<string> WriteAsync(short[] samples, string prefix)
    {
        var wav = new WavFile
        {
            Channels = 1,
            SampleRate = SampleRate,
            BitsPerSample = 16,
            Samples = samples,
        };

        var path = Path.Combine(SynthDirectory, $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmssfff}.wav");
        await wav.WriteAsync(path);
        return path;
    }
}
