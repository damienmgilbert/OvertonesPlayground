using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Services.Interfaces;
// IClipboard/IShare (Microsoft.Maui.ApplicationModel.DataTransfer) are already in scope via MAUI's implicit global usings.

namespace OvertonesPlayground.ViewModels;

///<summary>
///View model for the Speech-to-Text page: listens live through the microphone and shows partial/final transcripts as
///they arrive. Android's speech recognizer has no reliable way to transcribe an existing recording - only live
///listening - so there's no "pick a clip" option here; see the Phase 5 notes for why.
///</summary>
public sealed partial class SpeechToTextViewModel : BaseViewModel, IDisposable
{
    #region Fields
    private readonly IClipboard _clipboard;
    private readonly IPermissionsService _permissionsService;
    private readonly IShare _share;
    private readonly ISpeechToTextService _speechToTextService;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the view model and subscribes to the speech-to-text service's results for the lifetime of the app.
    ///</summary>
    public SpeechToTextViewModel(ISpeechToTextService speechToTextService, IPermissionsService permissionsService, IClipboard clipboard, IShare share, ILogger<SpeechToTextViewModel> logger) : base(logger)
    {
        _speechToTextService = speechToTextService;
        _permissionsService = permissionsService;
        _clipboard = clipboard;
        _share = share;
        Title = "Speech to Text";

        _speechToTextService.PartialResultReceived += OnPartialResultReceived;
        _speechToTextService.FinalResultReceived += OnFinalResultReceived;
        _speechToTextService.RecognitionError += OnRecognitionError;
    }
    #endregion

    #region Private methods
    ///<summary>
    ///Copies the current transcript to the clipboard.
    ///</summary>
    [RelayCommand]
    private async Task CopyAsync()
    {
        if (string.IsNullOrEmpty(Transcript))
        {
            return;
        }

        await _clipboard.SetTextAsync(Transcript);
        StatusMessage = "Copied to clipboard.";
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Microphone permission denied.")]
    private partial void Log_MicrophonePermissionDenied();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Speech recognition failed: {Message}")]
    private partial void Log_RecognitionError(string message);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting live speech recognition.")]
    private partial void Log_StartingListening();

    private void OnFinalResultReceived(object? sender, string transcript)
    {
        IsListening = false;
        Transcript = transcript;
        StatusMessage = string.IsNullOrWhiteSpace(transcript) ? "Didn't catch that - try again." : "Done listening.";
    }

    private void OnPartialResultReceived(object? sender, string transcript) => Transcript = transcript;

    private void OnRecognitionError(object? sender, string message)
    {
        IsListening = false;
        Log_RecognitionError(message);
        StatusMessage = message;
    }

    ///<summary>
    ///Shares the current transcript via the platform share sheet.
    ///</summary>
    [RelayCommand]
    private async Task ShareAsync()
    {
        if (string.IsNullOrEmpty(Transcript))
        {
            return;
        }

        await _share.RequestAsync(new ShareTextRequest { Text = Transcript, Title = "Share transcript", });
    }

    ///<summary>
    ///Checks recognizer availability and microphone permission, then starts listening.
    ///</summary>
    [RelayCommand]
    private async Task StartListeningAsync()
    {
        if (IsListening)
        {
            return;
        }

        if (!_speechToTextService.IsRecognitionAvailable())
        {
            StatusMessage = "Speech recognition isn't available on this device (common on emulators without Google Play Services).";
            return;
        }

        bool granted = await _permissionsService.EnsureMicrophonePermissionAsync();
        if (!granted)
        {
            Log_MicrophonePermissionDenied();
            StatusMessage = "Microphone permission is required to listen.";
            return;
        }

        Log_StartingListening();
        Transcript = string.Empty;
        StatusMessage = "Listening...";
        IsListening = true;
        _speechToTextService.StartListening();
    }

    ///<summary>
    ///Stops listening early; whatever was captured still arrives as a final result.
    ///</summary>
    [RelayCommand]
    private void StopListening() => _speechToTextService.StopListening();
    #endregion

    #region Public methods
    ///<summary>
    ///Stops a live session, if one is running. Called when the app is stopped: Android does not deliver microphone audio to
    ///a background app, so the session could only sit "listening" to silence until it timed out.
    ///</summary>
    public void StopListeningIfActive()
    {
        if (IsListening)
        {
            StopListening();
        }
    }

    ///<summary>
    ///Unsubscribes from the speech-to-text service.
    ///</summary>
    public void Dispose()
    {
        _speechToTextService.PartialResultReceived -= OnPartialResultReceived;
        _speechToTextService.FinalResultReceived -= OnFinalResultReceived;
        _speechToTextService.RecognitionError -= OnRecognitionError;
        GC.SuppressFinalize(this);
    }
    #endregion

    #region Public properties
    ///<summary>
    ///Whether the recognizer is currently listening.
    ///</summary>
    [ObservableProperty]
    public partial bool IsListening { get; set; }

    ///<summary>
    ///The current (partial or final) transcript.
    ///</summary>
    [ObservableProperty]
    public partial string Transcript { get; set; } = string.Empty;
    #endregion
}
