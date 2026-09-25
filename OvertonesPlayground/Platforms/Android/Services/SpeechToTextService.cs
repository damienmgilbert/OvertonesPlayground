using Android.Content;
using Android.OS;
using Android.Speech;
using OvertonesPlayground.Services.Interfaces;
using AndroidApp = Android.App.Application;

namespace OvertonesPlayground.Platforms.Android.Services;

///<summary>
///Live speech-to-text via Android's <see cref="SpeechRecognizer"/>. There's no public, reliable API to feed the
///recognizer a pre-recorded file - it's built entirely around listening to the live microphone - so this only
///supports that: start listening, report partial results as they arrive, then a final transcript.
///</summary>
public sealed class SpeechToTextService : ISpeechToTextService, IDisposable
{
    #region Fields
    private SpeechRecognizer? _recognizer;
    #endregion

    #region Events
    ///<inheritdoc/>
    public event EventHandler<string>? FinalResultReceived;

    ///<inheritdoc/>
    public event EventHandler<string>? PartialResultReceived;

    ///<inheritdoc/>
    public event EventHandler<string>? RecognitionError;
    #endregion

    #region Private methods
    ///<summary>
    ///Translates a <see cref="SpeechRecognizerError"/> into a short, human-readable message for
    ///<see cref="RecognitionError"/>.
    ///</summary>
    // A handful of SpeechRecognizerError members are annotated as API 31+; comparing against them is a plain int
    // comparison (no platform API call), so it's safe on every OS version this app supports - CA1416 doesn't see that.
#pragma warning disable CA1416
    private static string DescribeError(SpeechRecognizerError error) => error switch
    {
        SpeechRecognizerError.Audio => "A microphone/audio error occurred.",
        SpeechRecognizerError.CannotCheckSupport => "Couldn't check speech recognition support.",
        SpeechRecognizerError.CannotListenToDownloadEvents => "Couldn't listen for language model download events.",
        SpeechRecognizerError.Client => "The recognizer reported a client-side error.",
        SpeechRecognizerError.InsufficientPermissions => "Microphone permission is required to listen.",
        SpeechRecognizerError.LanguageNotSupported => "This device's speech engine doesn't support the current language.",
        SpeechRecognizerError.LanguageUnavailable => "The requested language isn't available on this device.",
        SpeechRecognizerError.Network => "A network error interrupted speech recognition.",
        SpeechRecognizerError.NetworkTimeout => "Speech recognition timed out waiting for the network.",
        SpeechRecognizerError.NoMatch => "Didn't catch that - no speech was recognized.",
        SpeechRecognizerError.RecognizerBusy => "The speech recognizer is busy; try again in a moment.",
        SpeechRecognizerError.Server => "The speech recognition server returned an error.",
        SpeechRecognizerError.ServerDisconnected => "Lost connection to the speech recognition server.",
        SpeechRecognizerError.SpeechTimeout => "No speech was detected.",
        SpeechRecognizerError.TooManyRequests => "Too many speech recognition requests; try again shortly.",
        _ => "Speech recognition failed.",
    };
#pragma warning restore CA1416

    ///<summary>
    ///Pulls the recognizer's top-ranked transcript out of a results/partial-results <see cref="Bundle"/>.
    ///</summary>
    private static string GetBestTranscript(Bundle? results)
    {
        IList<string>? matches = results?.GetStringArrayList(SpeechRecognizer.ResultsRecognition);
        return matches is { Count: > 0 } && matches[0] is { } best ? best : string.Empty;
    }

    private void OnError(object? sender, global::Android.Speech.ErrorEventArgs e)
    {
        IsListening = false;
        string message = DescribeError(e.Error);
        TeardownRecognizer();
        RecognitionError?.Invoke(this, message);
    }

    private void OnPartialResults(object? sender, PartialResultsEventArgs e) => PartialResultReceived?.Invoke(this, GetBestTranscript(e.PartialResults));

    private void OnResults(object? sender, ResultsEventArgs e)
    {
        IsListening = false;
        string transcript = GetBestTranscript(e.Results);
        TeardownRecognizer();
        FinalResultReceived?.Invoke(this, transcript);
    }

    ///<summary>
    ///Unsubscribes and destroys the current recognizer, if any, so a fresh one is created next time listening starts.
    ///</summary>
    private void TeardownRecognizer()
    {
        if (_recognizer is null)
        {
            return;
        }

        _recognizer.Error -= OnError;
        _recognizer.Results -= OnResults;
        _recognizer.PartialResults -= OnPartialResults;
        _recognizer.Destroy();
        _recognizer.Dispose();
        _recognizer = null;
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Tears down any in-progress recognizer.
    ///</summary>
    public void Dispose() => TeardownRecognizer();

    ///<inheritdoc/>
    public bool IsRecognitionAvailable() => SpeechRecognizer.IsRecognitionAvailable(AndroidApp.Context);

    ///<inheritdoc/>
    public void StartListening()
    {
        if (IsListening)
        {
            return;
        }

        if (!IsRecognitionAvailable())
        {
            RecognitionError?.Invoke(this, "Speech recognition isn't available on this device (common on emulators without Google Play Services).");
            return;
        }

        // Assign straight to the field so ownership lives there from the start; TeardownRecognizer disposes it.
        _recognizer = SpeechRecognizer.CreateSpeechRecognizer(AndroidApp.Context);
        if (_recognizer is null)
        {
            RecognitionError?.Invoke(this, "Couldn't create a speech recognizer on this device.");
            return;
        }

        _recognizer.Error += OnError;
        _recognizer.Results += OnResults;
        _recognizer.PartialResults += OnPartialResults;

        using Intent intent = new(RecognizerIntent.ActionRecognizeSpeech);
        intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
        intent.PutExtra(RecognizerIntent.ExtraLanguage, Java.Util.Locale.Default.ToString());
        intent.PutExtra(RecognizerIntent.ExtraPartialResults, true);
        intent.PutExtra(RecognizerIntent.ExtraCallingPackage, AndroidApp.Context.PackageName);

        _recognizer.StartListening(intent);
        IsListening = true;
    }

    ///<inheritdoc/>
    public void StopListening() => _recognizer?.StopListening();
    #endregion

    #region Public properties
    ///<inheritdoc/>
    public bool IsListening { get; private set; }
    #endregion
}
