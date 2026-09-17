using OvertonesPlayground.Models;

namespace OvertonesPlayground.Services.Interfaces;

///<summary>
///Generates audio from scratch - tones and drum-machine one-shots - rather than editing or recording existing files.
///The returned clip points at a WAV file in app-private scratch storage and is NOT added to the library; callers decide
///whether to keep it via <see cref="IAudioLibraryService.AddClipAsync"/>.
///</summary>
public interface ISoundSynthesisService
{
    #region Public methods
    ///<summary>
    ///Synthesizes a drum-machine one-shot using the given type and tunable parameters.
    ///</summary>
    Task<AudioClip> GenerateDrumAsync(DrumType drum, string name, DrumSynthParameters parameters);

    ///<summary>
    ///Synthesizes a single oscillator tone with the given waveform, pitch, length, and level.
    ///</summary>
    Task<AudioClip> GenerateToneAsync(WaveformType waveform, double frequencyHz, double durationSeconds, double amplitude, string name);
    #endregion
}
