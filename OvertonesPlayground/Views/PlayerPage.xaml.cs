using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

///<summary>
///Code-behind for the Player page: manages the position-refresh timer's lifecycle and the seek slider.
///</summary>
public partial class PlayerPage : ContentPage
{
    #region Fields
    private readonly ILogger<PlayerPage> _logger;
    private readonly PlayerViewModel _viewModel;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the page and binds it to its view model.
    ///</summary>
    public PlayerPage(PlayerViewModel viewModel, ILogger<PlayerPage> logger)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _logger = logger;
    }
    #endregion

    #region Private methods
    ///<summary>
    ///Applies the seek slider's dropped position to the playback service.
    ///</summary>
    private void OnSeekCompleted(object? sender, EventArgs e) { _viewModel.SeekCommand.Execute(PositionSlider.Value); }
    #endregion

    #region Protected methods
    ///<summary>
    ///Starts the position-refresh timer while the page is visible.
    ///</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _logger.LogDebug("Page appeared.");
        _viewModel.StartTicking(Dispatcher);
    }

    ///<summary>
    ///Stops the position-refresh timer once the page is no longer visible.
    ///</summary>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _logger.LogDebug("Page disappeared.");
        _viewModel.StopTicking();
    }
    #endregion
}
