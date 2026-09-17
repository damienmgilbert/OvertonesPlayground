using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

public partial class MixerPage : ContentPage
{
    public MixerPage(MixerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
