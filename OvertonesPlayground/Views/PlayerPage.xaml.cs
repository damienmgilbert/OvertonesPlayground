using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

public partial class PlayerPage : ContentPage
{
    private readonly PlayerViewModel _viewModel;

    public PlayerPage(PlayerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.StartTicking(Dispatcher);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.StopTicking();
    }

    private void OnSeekCompleted(object? sender, EventArgs e) =>
        _viewModel.SeekCommand.Execute(PositionSlider.Value);
}
