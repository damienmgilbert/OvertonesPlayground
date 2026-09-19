using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

///<summary>
///Code-behind for the Audio Recorder page; all behavior lives in <see cref="SoundCreatorViewModel"/>.
///</summary>
public partial class SoundCreatorPage : ContentPage
{
    #region Fields
    private readonly ILogger<SoundCreatorPage> _logger;
    private Window? _window;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the page and binds it to its view model.
    ///</summary>
    public SoundCreatorPage(SoundCreatorViewModel viewModel, ILogger<SoundCreatorPage> logger)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _logger = logger;
    }
    #endregion

    #region Private methods
    private async void OnWindowStopped(object? sender, EventArgs e)
    {
        if (BindingContext is SoundCreatorViewModel viewModel)
        {
            await viewModel.FinishRecordingAsync().ConfigureAwait(true);
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

        // The app can also be stopped while this page is showing (Home button, another app): save the take then too.
        _window = Window;
        _window?.Stopped += OnWindowStopped;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Log_PageDisappeared();

        _window?.Stopped -= OnWindowStopped;
        _window = null;

        // Leaving the page mid-recording would otherwise leave the recorder running with nothing on screen to stop it.
        if (BindingContext is SoundCreatorViewModel viewModel)
        {
            _ = viewModel.FinishRecordingAsync();
        }
    }
    #endregion
}
