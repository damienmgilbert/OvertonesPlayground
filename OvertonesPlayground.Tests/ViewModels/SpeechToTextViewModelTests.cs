namespace OvertonesPlayground.Tests.ViewModels;

public sealed class SpeechToTextViewModelTests
{
    #region Fields
    private readonly IClipboard _clipboard = Substitute.For<IClipboard>();
    private readonly IPermissionsService _permissions = Substitute.For<IPermissionsService>();
    private readonly IShare _share = Substitute.For<IShare>();
    private readonly ISpeechToTextService _speech = Substitute.For<ISpeechToTextService>();
    #endregion

    #region Private methods
    private SpeechToTextViewModel Create()
    {
        _speech.IsRecognitionAvailable().Returns(true);
        _permissions.EnsureMicrophonePermissionAsync().Returns(true);
        return new(_speech, _permissions, _clipboard, _share, NullLogger<SpeechToTextViewModel>.Instance);
    }

    private void RaiseFinal(string text) => _speech.FinalResultReceived += Raise.Event<EventHandler<string>>(_speech, text);

    private void RaisePartial(string text) => _speech.PartialResultReceived += Raise.Event<EventHandler<string>>(_speech, text);
    #endregion

    #region Public methods
    [Fact]
    public async Task Copy_EmptyTranscript_DoesNothing()
    {
        SpeechToTextViewModel viewModel = Create();

        await viewModel.CopyCommand.ExecuteAsync(null);

        await _clipboard.DidNotReceiveWithAnyArgs().SetTextAsync(default);
        Assert.Null(viewModel.StatusMessage);
    }

    [Fact]
    public async Task Copy_PutsTheTranscriptOnTheClipboard()
    {
        SpeechToTextViewModel viewModel = Create();
        viewModel.Transcript = "some words";

        await viewModel.CopyCommand.ExecuteAsync(null);

        await _clipboard.Received(1).SetTextAsync("some words");
        Assert.Equal("Copied to clipboard.", viewModel.StatusMessage);
    }

    [Fact]
    public void Dispose_StopsReactingToTheRecognizer()
    {
        SpeechToTextViewModel viewModel = Create();

        viewModel.Dispose();
        RaisePartial("too late");
        RaiseFinal("too late");

        Assert.Equal(string.Empty, viewModel.Transcript);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FinalResult_NothingHeard_AsksToTryAgain(string transcript)
    {
        SpeechToTextViewModel viewModel = Create();

        RaiseFinal(transcript);

        Assert.Equal("Didn't catch that - try again.", viewModel.StatusMessage);
    }

    [Fact]
    public void FinalResult_StopsListeningAndKeepsTheTranscript()
    {
        SpeechToTextViewModel viewModel = Create();
        viewModel.IsListening = true;

        RaiseFinal("hello world");

        Assert.False(viewModel.IsListening);
        Assert.Equal("hello world", viewModel.Transcript);
        Assert.Equal("Done listening.", viewModel.StatusMessage);
    }

    [Fact]
    public void PartialResult_UpdatesTheTranscriptWhileStillListening()
    {
        SpeechToTextViewModel viewModel = Create();
        viewModel.IsListening = true;

        RaisePartial("hello wor");

        Assert.Equal("hello wor", viewModel.Transcript);
        Assert.True(viewModel.IsListening);
    }

    [Fact]
    public void RecognitionError_StopsListeningAndShowsTheMessage()
    {
        SpeechToTextViewModel viewModel = Create();
        viewModel.IsListening = true;

        _speech.RecognitionError += Raise.Event<EventHandler<string>>(_speech, "Network error");

        Assert.False(viewModel.IsListening);
        Assert.Equal("Network error", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Share_EmptyTranscript_DoesNothing()
    {
        await Create().ShareCommand.ExecuteAsync(null);

        await _share.DidNotReceiveWithAnyArgs().RequestAsync(default(ShareTextRequest)!);
    }

    [Fact]
    public async Task Share_OffersTheTranscriptAsText()
    {
        SpeechToTextViewModel viewModel = Create();
        viewModel.Transcript = "some words";

        await viewModel.ShareCommand.ExecuteAsync(null);

        await _share.Received(1).RequestAsync(Arg.Is<ShareTextRequest>(request => request.Text == "some words" && request.Title == "Share transcript"));
    }

    [Fact]
    public async Task StartListening_AlreadyListening_DoesNotStartAgain()
    {
        SpeechToTextViewModel viewModel = Create();
        await viewModel.StartListeningCommand.ExecuteAsync(null);

        await viewModel.StartListeningCommand.ExecuteAsync(null);

        _speech.Received(1).StartListening();
    }

    [Fact]
    public async Task StartListening_EverythingAvailable_StartsListeningWithAFreshTranscript()
    {
        SpeechToTextViewModel viewModel = Create();
        viewModel.Transcript = "left over";

        await viewModel.StartListeningCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsListening);
        Assert.Equal(string.Empty, viewModel.Transcript);
        Assert.Equal("Listening...", viewModel.StatusMessage);
        _speech.Received(1).StartListening();
    }

    [Fact]
    public async Task StartListening_MicrophonePermissionDenied_ExplainsAndDoesNotStart()
    {
        SpeechToTextViewModel viewModel = Create();
        _permissions.EnsureMicrophonePermissionAsync().Returns(false);

        await viewModel.StartListeningCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsListening);
        Assert.Equal("Microphone permission is required to listen.", viewModel.StatusMessage);
        _speech.DidNotReceive().StartListening();
    }

    [Fact]
    public async Task StartListening_RecognizerUnavailable_ExplainsAndDoesNotStart()
    {
        SpeechToTextViewModel viewModel = Create();
        _speech.IsRecognitionAvailable().Returns(false);

        await viewModel.StartListeningCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsListening);
        Assert.Contains("isn't available", viewModel.StatusMessage);
        _speech.DidNotReceive().StartListening();
        await _permissions.DidNotReceive().EnsureMicrophonePermissionAsync();
    }

    [Fact]
    public void StopListening_AsksTheRecognizerToStop()
    {
        Create().StopListeningCommand.Execute(null);

        _speech.Received(1).StopListening();
    }

    [Fact]
    public void StopListeningIfActive_WhileIdle_DoesNothing()
    {
        Create().StopListeningIfActive();

        _speech.DidNotReceive().StopListening();
    }

    [Fact]
    public void StopListeningIfActive_WhileListening_Stops()
    {
        SpeechToTextViewModel viewModel = Create();
        viewModel.IsListening = true;

        viewModel.StopListeningIfActive();

        _speech.Received(1).StopListening();
    }
    #endregion
}
