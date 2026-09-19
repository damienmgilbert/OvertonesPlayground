using System.ComponentModel;
using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

///<summary>
///Code-behind for the Sound Editor page: wires the waveform view to the view model's peaks.
///</summary>
public partial class AudioEditorPage : ContentPage
{
    #region Fields
    private readonly WaveformDrawable _drawable = new();
    private readonly ILogger<AudioEditorPage> _logger;
    private readonly AudioEditorViewModel _viewModel;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the page and hooks up its waveform drawable.
    ///</summary>
    public AudioEditorPage(AudioEditorViewModel viewModel, ILogger<AudioEditorPage> logger)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _logger = logger;

        WaveformView.Drawable = _drawable;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }
    #endregion

    #region Private methods
    [LoggerMessage(Level = LogLevel.Debug, Message = "Page appeared.")]
    private partial void Log_PageAppeared();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Page disappeared.")]
    private partial void Log_PageDisappeared();

    ///<summary>
    ///Redraws the waveform whenever the view model loads a new clip's peaks.
    ///</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AudioEditorViewModel.WaveformPeaks))
        {
            _drawable.Peaks = _viewModel.WaveformPeaks;
            WaveformView.Invalidate();
        }
    }

    ///<summary>
    ///Repaints the waveform when the theme changes: the drawable reads its colors from the Fluent tokens as it draws.
    ///</summary>
    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e) => WaveformView.Invalidate();
    #endregion

    #region Protected methods
    protected override void OnAppearing()
    {
        base.OnAppearing();
        Application.Current!.RequestedThemeChanged += OnRequestedThemeChanged;
        Log_PageAppeared();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Application.Current!.RequestedThemeChanged -= OnRequestedThemeChanged;
        Log_PageDisappeared();
    }
    #endregion
}
