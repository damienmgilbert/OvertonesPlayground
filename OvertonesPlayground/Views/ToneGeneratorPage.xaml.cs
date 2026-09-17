using System.ComponentModel;
using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

public partial class ToneGeneratorPage : ContentPage
{
    private readonly ToneGeneratorViewModel _viewModel;
    private readonly WaveformDrawable _drawable = new();

    public ToneGeneratorPage(ToneGeneratorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        WaveformView.Drawable = _drawable;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ToneGeneratorViewModel.WaveformPeaks))
        {
            _drawable.Peaks = _viewModel.WaveformPeaks;
            WaveformView.Invalidate();
        }
    }
}
