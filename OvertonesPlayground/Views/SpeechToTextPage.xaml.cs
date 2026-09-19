using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

///<summary>
///Code-behind for the Speech to Text page; all behavior lives in <see cref="SpeechToTextViewModel"/>.
///</summary>
public partial class SpeechToTextPage : ContentPage
{
    #region Fields
    private readonly ILogger<SpeechToTextPage> _logger;
    private bool _stopHooked;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the page and binds it to its view model.
    ///</summary>
    public SpeechToTextPage(SpeechToTextViewModel viewModel, ILogger<SpeechToTextPage> logger)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _logger = logger;
    }
    #endregion

    #region Private methods
    private void OnWindowStopped(object? sender, EventArgs e)
    {
        if (BindingContext is SpeechToTextViewModel viewModel)
        {
            viewModel.StopListeningIfActive();
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Page appeared.")]
    private partial void Log_PageAppeared();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Page disappeared.")]
    private partial void Log_PageDisappeared();
    #endregion

    #region Protected methods
    protected override void OnAppearing()
    {
        base.OnAppearing();
        Log_PageAppeared();

        // Listening deliberately continues while you visit another page, so this is hooked once for the life of the app
        // (this page is a singleton and the app has a single window) rather than per appearance.
        if (!_stopHooked && Window is { } window)
        {
            window.Stopped += OnWindowStopped;
            _stopHooked = true;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Log_PageDisappeared();
    }
    #endregion
}
