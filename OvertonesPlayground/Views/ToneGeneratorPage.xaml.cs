using System.ComponentModel;
using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

/// <summary>Code-behind for the Tone Generator page: wires the waveform view to the view model's peaks.</summary>
public partial class ToneGeneratorPage : ContentPage
{
    private readonly ToneGeneratorViewModel _viewModel;
    private readonly WaveformDrawable _drawable = new();

    /// <summary>Creates the page and hooks up its waveform drawable.</summary>
    public ToneGeneratorPage(ToneGeneratorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        WaveformView.Drawable = _drawable;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <summary>Redraws the waveform whenever the view model generates a new preview.</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ToneGeneratorViewModel.WaveformPeaks))
        {
            _drawable.Peaks = _viewModel.WaveformPeaks;
            WaveformView.Invalidate();
        }
    }
}
