using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

/// <summary>Code-behind for the Launchpad page; all behavior lives in <see cref="LaunchpadViewModel"/>.</summary>
public partial class LaunchpadPage : ContentPage
{
    /// <summary>Creates the page and binds it to its view model.</summary>
    public LaunchpadPage(LaunchpadViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
