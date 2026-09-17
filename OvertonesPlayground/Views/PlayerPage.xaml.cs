using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

/// <summary>Code-behind for the Player page: manages the position-refresh timer's lifecycle and the seek slider.</summary>
public partial class PlayerPage : ContentPage
{
    private readonly PlayerViewModel _viewModel;

    /// <summary>Creates the page and binds it to its view model.</summary>
    public PlayerPage(PlayerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    /// <summary>Starts the position-refresh timer while the page is visible.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.StartTicking(Dispatcher);
    }

    /// <summary>Stops the position-refresh timer once the page is no longer visible.</summary>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.StopTicking();
    }

    /// <summary>Applies the seek slider's dropped position to the playback service.</summary>
    private void OnSeekCompleted(object? sender, EventArgs e) =>
        _viewModel.SeekCommand.Execute(PositionSlider.Value);
}
