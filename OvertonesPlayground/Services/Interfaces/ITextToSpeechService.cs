using OvertonesPlayground.Models;

namespace OvertonesPlayground.Services.Interfaces;

///<summary>
///Synthesizes speech audio from text using the platform's text-to-speech engine. The returned clip points at a
///16-bit PCM WAV file in app-private scratch storage and is NOT added to the library; callers decide whether to keep
///it via <see cref="IAudioLibraryService.AddClipAsync"/>.
///</summary>
public interface ITextToSpeechService
{
    #region Public methods
    ///<summary>
    ///Synthesizes <paramref name="text"/> into a new 16-bit PCM WAV file and returns a clip pointing at it.
    ///</summary>
    ///<exception cref="ArgumentException"><paramref name="text"/> or <paramref name="name"/> is null, empty, or
    ///whitespace.</exception>
    ///<exception cref="NotSupportedException">No text-to-speech engine is available on this device, the engine
    ///doesn't support the current language, or it produced something other than a 16-bit PCM WAV file.</exception>
    Task<AudioClip> SynthesizeAsync(string text, string name);
    #endregion
}
