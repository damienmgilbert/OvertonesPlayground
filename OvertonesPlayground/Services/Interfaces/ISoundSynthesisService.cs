using OvertonesPlayground.Models;

namespace OvertonesPlayground.Services.Interfaces;

/// <summary>
/// Generates audio from scratch - tones and drum-machine one-shots - rather than editing or
/// recording existing files. Every generated sound is written to a WAV file and registered in
/// the library so it's immediately usable in the Launchpad and Mixer.
/// </summary>
public interface ISoundSynthesisService
{
    Task<AudioClip> GenerateToneAsync(
        WaveformType waveform, double frequencyHz, double durationSeconds, double amplitude, string name);

    Task<AudioClip> GenerateDrumAsync(DrumType drum, string name);
}
