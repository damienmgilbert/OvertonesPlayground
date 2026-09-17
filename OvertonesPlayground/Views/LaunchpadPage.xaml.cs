using Microsoft.Extensions.Logging;
using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

///<summary>
///Code-behind for the Launchpad page; all behavior lives in <see cref="LaunchpadViewModel"/>.
///</summary>
public partial class LaunchpadPage : ContentPage
{
    #region Fields
    private readonly ILogger<LaunchpadPage> _logger;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the page and binds it to its view model.
    ///</summary>
    public LaunchpadPage(LaunchpadViewModel viewModel, ILogger<LaunchpadPage> logger)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _logger = logger;
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
