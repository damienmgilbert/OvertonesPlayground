using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.Services.Implementations;

public class SoundSynthesisService : ISoundSynthesisService
{
    private const int SampleRate = 44_100;

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
        return await WriteAsync(samples, SampleRate, $"tone_{waveform}", name);
    }

    public async Task<AudioClip> GenerateDrumAsync(DrumType drum, string name, DrumSynthParameters parameters)
    {
        var samples = drum switch
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

        return await WriteAsync(samples, parameters.SampleRate, $"drum_{drum}", name);
    }

    private static async Task<AudioClip> WriteAsync(short[] samples, int sampleRate, string prefix, string name)
    {
        var wav = new WavFile
        {
            Channels = 1,
            SampleRate = sampleRate,
            BitsPerSample = 16,
            Samples = samples,
        };

        var path = Path.Combine(SynthDirectory, $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmssfff}.wav");
        await wav.WriteAsync(path);

        return new AudioClip
        {
            Name = name,
            FilePath = path,
            Duration = wav.Duration,
            IsUserRecording = true,
        };
    }
}
