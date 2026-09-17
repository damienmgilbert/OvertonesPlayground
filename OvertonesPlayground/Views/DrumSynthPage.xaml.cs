using System.ComponentModel;
using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

public partial class DrumSynthPage : ContentPage
{
    private readonly DrumSynthViewModel _viewModel;
    private readonly WaveformDrawable _drawable = new();

    public DrumSynthPage(DrumSynthViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        WaveformView.Drawable = _drawable;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DrumSynthViewModel.WaveformPeaks))
        {
            _drawable.Peaks = _viewModel.WaveformPeaks;
            WaveformView.Invalidate();
        }
    }
}
