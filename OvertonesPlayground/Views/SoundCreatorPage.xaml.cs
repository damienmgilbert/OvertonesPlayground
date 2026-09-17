using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

///<summary>
///Code-behind for the Audio Recorder page; all behavior lives in <see cref="SoundCreatorViewModel"/>.
///</summary>
public partial class SoundCreatorPage : ContentPage
{
    #region Fields
    private readonly ILogger<SoundCreatorPage> _logger;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the page and binds it to its view model.
    ///</summary>
    public SoundCreatorPage(SoundCreatorViewModel viewModel, ILogger<SoundCreatorPage> logger)
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
