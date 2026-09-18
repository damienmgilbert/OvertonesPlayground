using System.ComponentModel;
using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

///<summary>
///Code-behind for the Tone Generator page: wires the waveform view to the view model's peaks.
///</summary>
public partial class ToneGeneratorPage : ContentPage
{
    #region Fields
    private readonly WaveformDrawable _drawable = new();
    private readonly ILogger<ToneGeneratorPage> _logger;
    private readonly ToneGeneratorViewModel _viewModel;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the page and hooks up its waveform drawable.
    ///</summary>
    public ToneGeneratorPage(ToneGeneratorViewModel viewModel, ILogger<ToneGeneratorPage> logger)
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
    ///Redraws the waveform whenever the view model generates a new preview.
    ///</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ToneGeneratorViewModel.WaveformPeaks))
        {
            _drawable.Peaks = _viewModel.WaveformPeaks;
            WaveformView.Invalidate();
        }
    }
    #endregion

    #region Protected methods
    protected override void OnAppearing()
    {
        base.OnAppearing();
        Log_PageAppeared();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Log_PageDisappeared();
    }
    #endregion
}
