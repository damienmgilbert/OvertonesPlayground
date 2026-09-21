using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

///<summary>
///Code-behind for the Settings page; all behavior lives in <see cref="SettingsViewModel"/>.
///</summary>
public partial class SettingsPage : ContentPage
{
    #region Fields
    private readonly ILogger<SettingsPage> _logger;
    private readonly SettingsViewModel _viewModel;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the page and binds it to its view model.
    ///</summary>
    public SettingsPage(SettingsViewModel viewModel, ILogger<SettingsPage> logger)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _logger = logger;
    }
    #endregion

    #region Private methods
    ///<summary>
    ///Asks before clearing the library, because the clips and their files can't be brought back.
    ///</summary>
    private async void OnClearLibraryClicked(object? sender, EventArgs e)
    {
        int count = await _viewModel.CountClipsAsync();
        if (count == 0)
        {
            _viewModel.StatusMessage = "Your library is already empty.";
            return;
        }

        string clips = count == 1 ? "1 clip" : $"{count} clips";
        bool confirmed = await DisplayAlertAsync("Clear library?", $"This permanently deletes {clips} and their audio files. It can't be undone.", "Delete all", "Cancel");
        if (confirmed)
        {
            await _viewModel.ClearLibraryCommand.ExecuteAsync(null);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Page appeared.")]
    private partial void Log_PageAppeared();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Page disappeared.")]
    private partial void Log_PageDisappeared();
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
