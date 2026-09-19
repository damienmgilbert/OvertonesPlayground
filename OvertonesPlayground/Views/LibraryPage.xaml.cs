using OvertonesPlayground.Models;
using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

///<summary>
///Code-behind for the Library page: loads the clip catalog each time the page appears, and hosts the "Export as..."
///format picker, which needs a native action sheet and so can't live in the view model.
///</summary>
public partial class LibraryPage : ContentPage
{
    #region Fields
    private readonly ILogger<LibraryPage> _logger;
    private readonly LibraryViewModel _viewModel;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the page and binds it to its view model.
    ///</summary>
    public LibraryPage(LibraryViewModel viewModel, ILogger<LibraryPage> logger)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _logger = logger;
    }
    #endregion

    #region Private methods
    [LoggerMessage(Level = LogLevel.Debug, Message = "Page appeared.")]
    private partial void Log_PageAppeared();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Page disappeared.")]
    private partial void Log_PageDisappeared();

    ///<summary>
    ///Prompts for an export format and, if one is chosen, encodes the tapped clip and exports it to shared storage.
    ///</summary>
    private async void OnExportClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: AudioClip clip })
        {
            return;
        }

        string? choice = await DisplayActionSheetAsync("Export as...", "Cancel", null, "AAC (recommended)", "MP3 (best-effort)");
        AudioExportFormat? format = choice switch
        {
            "AAC (recommended)" => AudioExportFormat.Aac,
            "MP3 (best-effort)" => AudioExportFormat.Mp3,
            _ => null,
        };

        if (format is not null)
        {
            await _viewModel.ExportClipAsync(clip, format.Value);
        }
    }
    #endregion

    #region Protected methods
    ///<summary>
    ///Refreshes the clip list every time the page becomes visible.
    ///</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        Log_PageAppeared();
        _viewModel.LoadCommand.Execute(null);

        // Show the one-time tip once the page has settled in (its entrance animation is 250 ms).
        _ = Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(500), () => _ = LibraryTip.ShowOnceAsync());
    }

    ///<summary>
    ///Logs when the page is no longer visible.
    ///</summary>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Log_PageDisappeared();

        _ = LibraryTip.CloseAsync();
    }

    ///<summary>
    ///Back closes an open tip before it leaves the page.
    ///</summary>
    protected override bool OnBackButtonPressed()
    {
        if (LibraryTip.IsOpen)
        {
            _ = LibraryTip.CloseAsync();
            return true;
        }

        return base.OnBackButtonPressed();
    }
    #endregion
}
