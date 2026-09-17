using System.ComponentModel;
using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

///<summary>
///Code-behind for the Drum Synth page: wires the waveform view to the view model's peaks.
///</summary>
public partial class DrumSynthPage : ContentPage
{
    #region Fields
    private readonly WaveformDrawable _drawable = new();
    private readonly ILogger<DrumSynthPage> _logger;
    private readonly DrumSynthViewModel _viewModel;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the page and hooks up its waveform drawable.
    ///</summary>
    public DrumSynthPage(DrumSynthViewModel viewModel, ILogger<DrumSynthPage> logger)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _logger = logger;

        WaveformView.Drawable = _drawable;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }
    #endregion

    #region Private methods
    ///<summary>
    ///Redraws the waveform whenever the view model generates a new preview.
    ///</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if(e.PropertyName == nameof(DrumSynthViewModel.WaveformPeaks))
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
        _logger.LogDebug("Page appeared.");
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _logger.LogDebug("Page disappeared.");
    }
    #endregion
}
