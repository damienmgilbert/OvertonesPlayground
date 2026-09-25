using Android.Speech.Tts;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Implementations;
using OvertonesPlayground.Services.Interfaces;
using AndroidApp = Android.App.Application;
using JavaFile = Java.IO.File;
// Disambiguate against Microsoft.Maui.Media's same-named types, both in scope via MAUI's implicit global usings.
using Locale = Java.Util.Locale;
using TextToSpeech = Android.Speech.Tts.TextToSpeech;

namespace OvertonesPlayground.Platforms.Android.Services;

///<summary>
///Synthesizes speech using Android's <see cref="TextToSpeech"/> engine, bridging its callback-based init
///(<see cref="TextToSpeech.IOnInitListener"/>) and per-utterance completion (<see cref="UtteranceProgressListener"/>,
///via <see cref="SpeechSynthesisListener"/>) lifecycle into a single awaitable method - the same spirit as
///<c>AudioFormatConverterService</c>'s decoder pump loop. The engine writes 16-bit PCM WAV directly to the target
///file, so the result drops straight into the app's WAV-only pipeline with no extra conversion; reading it back with
///<see cref="WavFile.ReadAsync"/> doubles as a sanity check that it really did, rather than silently trusting the
///".wav" extension.
///</summary>
// IDisposable isn't listed explicitly - Java.Lang.Object already implements it, and CA1063 flags the redundancy.
public sealed class TextToSpeechService : Java.Lang.Object, ITextToSpeechService, TextToSpeech.IOnInitListener
{
    #region Constants
    ///<summary>Utterance id passed to every synthesis request; requests are serialized by <see cref="_lock"/>, so
    ///only one is ever in flight and a fixed id is enough to correlate it with its completion callback.</summary>
    private const string UtteranceId = "OvertonesPlayground.Speak";
    #endregion

    #region Fields
    private TextToSpeech? _engine;
    private TaskCompletionSource<bool>? _initCompletion;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private TaskCompletionSource<bool>? _synthesisCompletion;
    private SpeechSynthesisListener? _utteranceListener;
    #endregion

    #region Private methods
    ///<summary>
    ///Builds a timestamped, filename-sanitized ".wav" path for <paramref name="name"/> in an app-private "Speech"
    ///folder (created if missing).
    ///</summary>
    private static string BuildOutputPath(string name)
    {
        string dir = Path.Combine(FileSystem.AppDataDirectory, "Speech");
        Directory.CreateDirectory(dir);

        char[] invalid = Path.GetInvalidFileNameChars();
        string sanitized = string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
        return Path.Combine(dir, $"{sanitized}_{DateTime.Now:yyyyMMdd_HHmmssfff}.wav");
    }

    ///<summary>
    ///Lazily creates and initializes the TTS engine, awaiting <see cref="OnInit"/>'s callback the first time.
    ///</summary>
    private async Task<TextToSpeech> EnsureEngineAsync()
    {
        if (_engine is not null)
        {
            return _engine;
        }

        _initCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        TextToSpeech engine = new(AndroidApp.Context, this);
        bool initialized = await _initCompletion.Task;
        if (!initialized)
        {
            engine.Dispose();
            throw new NotSupportedException("No text-to-speech engine is available on this device.");
        }

        _utteranceListener?.Dispose();
        _utteranceListener = new SpeechSynthesisListener(success => _synthesisCompletion?.TrySetResult(success));
        engine.SetOnUtteranceProgressListener(_utteranceListener);
        _engine = engine;
        return _engine;
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Releases the underlying TTS engine, then chains into <see cref="Java.Lang.Object.Dispose()"/> (sealed there, so
    ///this intentionally hides rather than overrides it) to release the JNI peer too.
    ///</summary>
    public new void Dispose()
    {
        _engine?.Shutdown();
        _engine?.Dispose();
        _utteranceListener?.Dispose();
        _lock.Dispose();
        base.Dispose();
    }

    ///<summary>
    ///Android's <see cref="TextToSpeech.IOnInitListener"/> callback; signals whichever caller is waiting inside
    ///<see cref="EnsureEngineAsync"/>.
    ///</summary>
    public void OnInit(OperationResult status) => _initCompletion?.TrySetResult(status == OperationResult.Success);

    ///<inheritdoc/>
    public async Task<AudioClip> SynthesizeAsync(string text, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        await _lock.WaitAsync();
        try
        {
            TextToSpeech engine = await EnsureEngineAsync();

            Locale locale = Locale.Default;
            LanguageAvailableResult availability = engine.IsLanguageAvailable(locale);
            bool isLanguageUnsupported = availability is LanguageAvailableResult.MissingData or LanguageAvailableResult.NotSupported;
            if (isLanguageUnsupported)
            {
                throw new NotSupportedException($"The text-to-speech engine doesn't support '{locale.DisplayName}'.");
            }

            engine.SetLanguage(locale);

            string path = BuildOutputPath(name);
            using JavaFile file = new(path);

            _synthesisCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            OperationResult queued = engine.SynthesizeToFile(text, null, file, UtteranceId);
            if (queued != OperationResult.Success)
            {
                throw new NotSupportedException("The text-to-speech engine rejected the synthesis request.");
            }

            bool completedOk = await _synthesisCompletion.Task;
            if (!completedOk)
            {
                throw new NotSupportedException("Text-to-speech synthesis failed.");
            }

            WavFile wav = await WavFile.ReadAsync(path);
            return new AudioClip { Name = name, FilePath = path, Duration = wav.Duration, IsUserRecording = true, };
        }
        finally
        {
            _lock.Release();
        }
    }
    #endregion
}

///<summary>
///Bridges Android's <see cref="UtteranceProgressListener"/> callbacks into a single completion signal for
///<see cref="TextToSpeechService.SynthesizeAsync"/>'s one in-flight request at a time. Deliberately not a "file"
///type: Java-callable-wrapper generation names the generated .java file after the compiler-mangled type name, which
///for "file" types contains characters ("&lt;", "&gt;") that aren't valid in a Windows file path.
///</summary>
internal sealed class SpeechSynthesisListener : UtteranceProgressListener
{
    #region Fields
    private readonly Action<bool> _onCompleted;
    #endregion

    #region Constructors
    public SpeechSynthesisListener(Action<bool> onCompleted)
    {
        _onCompleted = onCompleted;
    }
    #endregion

    #region Public methods
    public override void OnDone(string? utteranceId) => _onCompleted(true);

    // The single-arg overload is still abstract on UtteranceProgressListener (must be overridden) despite being
    // marked Obsolete in favor of the errorCode overload below; both are implemented so neither API level is left
    // uncovered.
#pragma warning disable CS0672
    public override void OnError(string? utteranceId) => _onCompleted(false);
#pragma warning restore CS0672

    public override void OnError(string? utteranceId, TextToSpeechError errorCode) => _onCompleted(false);

    public override void OnStart(string? utteranceId)
    {
    }
    #endregion
}
