using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

public partial class LaunchpadPage : ContentPage
{
    public LaunchpadPage(LaunchpadViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
