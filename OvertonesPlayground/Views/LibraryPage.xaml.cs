using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

/// <summary>Code-behind for the Library page: loads the clip catalog each time the page appears.</summary>
public partial class LibraryPage : ContentPage
{
    private readonly LibraryViewModel _viewModel;

    /// <summary>Creates the page and binds it to its view model.</summary>
    public LibraryPage(LibraryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    /// <summary>Refreshes the clip list every time the page becomes visible.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }
}
