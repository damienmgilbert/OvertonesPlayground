using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

///<summary>
///Code-behind for the Launchpad page. Behavior lives in <see cref="LaunchpadViewModel"/>; this hosts the pad menu, which
///needs a native action sheet and so can't live in the view model.
///</summary>
public partial class LaunchpadPage : ContentPage
{
    #region Constants
    private const string AssignSampleChoice = "Assign sample...";
    private const string ClearPadChoice = "Clear pad";
    private const string LoopOffChoice = "Loop: turn off";
    private const string LoopOnChoice = "Loop: turn on";
    private const string StopPadChoice = "Stop this pad";
    #endregion

    #region Fields
    private readonly ILogger<LaunchpadPage> _logger;
    private readonly LaunchpadViewModel _viewModel;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the page, binds it to its view model, and listens for pad-menu requests.
    ///</summary>
    public LaunchpadPage(LaunchpadViewModel viewModel, ILogger<LaunchpadPage> logger)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _logger = logger;

        _viewModel.PadMenuRequested += OnPadMenuRequested;
    }
    #endregion

    #region Private methods
    [LoggerMessage(Level = LogLevel.Debug, Message = "Page appeared.")]
    private partial void Log_PageAppeared();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Page disappeared.")]
    private partial void Log_PageDisappeared();

    ///<summary>
    ///Opens the pad menu when a pad is long-pressed. The sender is the pad's border, whose binding context is the pad.
    ///</summary>
    private void OnPadLongPressed(object? sender, EventArgs e)
    {
        if (sender is Border { BindingContext: LaunchpadPadViewModel pad })
        {
            _viewModel.OpenPadMenuCommand.Execute(pad);
        }
    }

    ///<summary>
    ///Shows the action sheet for a pad and runs the chosen action. Only the actions that apply are offered: an empty pad can
    ///only be assigned.
    ///</summary>
    private async void OnPadMenuRequested(object? sender, LaunchpadPadEventArgs e)
    {
        LaunchpadPadViewModel pad = e.Pad;
        string title = pad.HasClip ? $"Pad {pad.Index + 1}: {pad.Label}" : $"Pad {pad.Index + 1} (empty)";

        List<string> choices = [AssignSampleChoice];
        if (pad.HasClip)
        {
            choices.Add(pad.IsLooping ? LoopOffChoice : LoopOnChoice);
            choices.Add(StopPadChoice);
        }

        string? chosen = await DisplayActionSheetAsync(title, "Cancel", pad.HasClip ? ClearPadChoice : null, [.. choices]);
        switch (chosen)
        {
            case AssignSampleChoice:
                await _viewModel.AssignCommand.ExecuteAsync(pad);
                break;
            case LoopOnChoice:
            case LoopOffChoice:
                _viewModel.ToggleLoopCommand.Execute(pad);
                break;
            case StopPadChoice:
                _viewModel.StopPadCommand.Execute(pad);
                break;
            case ClearPadChoice:
                _viewModel.ClearPadCommand.Execute(pad);
                break;
            default:
                break;
        }
    }
    #endregion

    #region Protected methods
    protected override void OnAppearing()
    {
        base.OnAppearing();
        Log_PageAppeared();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Log_PageDisappeared();
    }
    #endregion
}
