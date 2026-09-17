using System.ComponentModel;
using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

public partial class AudioEditorPage : ContentPage
{
    private readonly AudioEditorViewModel _viewModel;
    private readonly WaveformDrawable _drawable = new();

    public AudioEditorPage(AudioEditorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        WaveformView.Drawable = _drawable;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AudioEditorViewModel.WaveformPeaks))
        {
            _drawable.Peaks = _viewModel.WaveformPeaks;
            WaveformView.Invalidate();
        }
    }
}
