using System.ComponentModel;
using OvertonesPlayground.Themes;
using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

///<summary>
///Code-behind for the Audio Recorder page; all behavior lives in <see cref="SoundCreatorViewModel"/>.
///</summary>
public partial class SoundCreatorPage : ContentPage
{
    #region Fields
    private readonly ILogger<SoundCreatorPage> _logger;
    private bool _isPulsing;
    private Window? _window;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the page and binds it to its view model.
    ///</summary>
    public SoundCreatorPage(SoundCreatorViewModel viewModel, ILogger<SoundCreatorPage> logger)
    {
        InitializeComponent();
        ArgumentNullException.ThrowIfNull(viewModel);
        BindingContext = viewModel;
        _logger = logger;

        // The view model lives exactly as long as this page (both are transient), so this subscription can't outlive it.
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }
    #endregion

    #region Private methods
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SoundCreatorViewModel.IsRecording) || sender is not SoundCreatorViewModel viewModel)
        {
            return;
        }

        StopPulse();
        if (viewModel.IsRecording && !FluentMotion.IsReduced)
        {
            // A cue that a take is live, on top of the button turning red and saying "Stop Recording".
            _isPulsing = true;
            _ = FluentMotion.PulseAsync(RecordButton, () => _isPulsing);
        }
    }

    private void StopPulse()
    {
        _isPulsing = false;
        FluentMotion.Settle(RecordButton);
    }

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
        StopPulse();

        // Leaving the page mid-recording would otherwise leave the recorder running with nothing on screen to stop it.
        if (BindingContext is SoundCreatorViewModel viewModel)
        {
            _ = viewModel.FinishRecordingAsync();
        }
    }
    #endregion
}
