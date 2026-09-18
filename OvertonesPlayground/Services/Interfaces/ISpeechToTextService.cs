namespace OvertonesPlayground.Services.Interfaces;

///<summary>
///Live speech-to-text via the platform's speech recognizer. The recognizer is built entirely around listening to the
///live microphone - there's no public, reliable API to feed it a pre-recorded file - so this service only offers
///that: start listening, get partial results as they arrive, then a final transcript.
///</summary>
public interface ISpeechToTextService
{
    #region Events
    ///<summary>
    ///Raised when the recognizer reports a final transcript and stops listening.
    ///</summary>
    event EventHandler<string>? FinalResultReceived;

    ///<summary>
    ///Raised whenever the recognizer's best-guess partial transcript changes while listening.
    ///</summary>
    event EventHandler<string>? PartialResultReceived;

    ///<summary>
    ///Raised when listening stops because of an error, with a short, human-readable description.
    ///</summary>
    event EventHandler<string>? RecognitionError;
    #endregion

    #region Public methods
    ///<summary>
    ///Returns whether a speech recognition service is available on this device - commonly false on emulators without
    ///Google Play Services.
    ///</summary>
    bool IsRecognitionAvailable();

    ///<summary>
    ///Starts listening on the microphone for a single utterance. No-op if already listening.
    ///</summary>
    void StartListening();

    ///<summary>
    ///Stops listening early; the recognizer still reports whatever it captured via <see cref="FinalResultReceived"/>.
    ///</summary>
    void StopListening();
    #endregion

    #region Public properties
    ///<summary>
    ///Whether the recognizer is currently listening.
    ///</summary>
    bool IsListening { get; }
    #endregion
}
