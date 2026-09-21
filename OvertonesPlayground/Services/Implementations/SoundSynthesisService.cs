using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.Services.Implementations;

///<inheritdoc cref="ISoundSynthesisService"/>
public class SoundSynthesisService : ISoundSynthesisService
{
    #region Constants
    private const int SampleRate = 44_100;
    #endregion

    #region Fields
    private readonly IFileSystem _fileSystem;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the synthesis service.
    ///</summary>
    ///<param name="fileSystem">Locates the app-private folder that every generated preview WAV file is written to.</param>
    public SoundSynthesisService(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }
    #endregion

    #region Private methods
    ///<summary>
    ///Writes raw PCM samples out as a mono 16-bit WAV file and wraps the result in an unsaved <see cref="AudioClip"/>.
    ///</summary>
    private async Task<AudioClip> WriteAsync(short[] samples, int sampleRate, string prefix, string name)
    {
        WavFile wav = new() { Channels = 1, SampleRate = sampleRate, BitsPerSample = 16, Samples = samples, };

        // Through the shared writer, which never reuses a name: a millisecond timestamp alone is not unique, and two sounds
        // generated back to back would otherwise be written to the same file.
        string path = await DerivedAudioFileWriter.SaveAsync(wav, SynthDirectory, prefix);

        AudioClip audioClip = new() { Name = name, FilePath = path, Duration = wav.Duration, IsUserRecording = true, };
        return audioClip;
    }
    #endregion

    #region Private properties
    ///<summary>
    ///App-private folder where every generated preview WAV file is written.
    ///</summary>
    private string SynthDirectory
    {
        get
        {
            string dir = Path.Combine(_fileSystem.AppDataDirectory, "Synth");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
    #endregion

    #region Public methods
    ///<inheritdoc/>
    public Task<AudioClip> GenerateDrumAsync(DrumType drum, string name, DrumSynthParameters parameters) => AudioWork.RunAsync(async () =>
    {
        short[] samples = drum switch
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

        string prefix = $"drum_{drum}";
        AudioClip audioClip = await WriteAsync(samples, parameters.SampleRate, prefix, name);
        return audioClip;
    });

    ///<inheritdoc/>
    public Task<AudioClip> GenerateToneAsync(WaveformType waveform, double frequencyHz, double durationSeconds, double amplitude, string name) => AudioWork.RunAsync(async () =>
    {
        short[] samples = WaveformGenerator.Generate(waveform, frequencyHz, durationSeconds, SampleRate, amplitude);
        AudioClip audioClip = await WriteAsync(samples, SampleRate, $"tone_{waveform}", name);
        return audioClip;
    });
    #endregion
}
