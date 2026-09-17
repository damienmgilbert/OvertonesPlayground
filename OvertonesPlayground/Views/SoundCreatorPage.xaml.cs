using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

public partial class SoundCreatorPage : ContentPage
{
    public SoundCreatorPage(SoundCreatorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
