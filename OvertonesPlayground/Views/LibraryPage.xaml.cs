using System.Windows.Input;
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
        SizeChanged += OnPageSizeChanged;
    }
    #endregion

    #region Private methods
    ///<summary>
    ///Runs <paramref name="command"/> for the clip of the row that was tapped, as <see cref="Button.Command"/> would. The
    ///sender is the row's button or, in the tile view, its tap gesture; either way it inherits the row's clip as its binding context.
    ///</summary>
    ///<remarks>
    ///The row buttons used to bind their commands to the view model with an ancestor <c>RelativeSource</c>. When a row is
    ///recycled (on scrolling, and all at once when the list is reloaded, e.g. on coming back from the editor) MAUI re-evaluates
    ///that binding with no ancestor, which throws a <see cref="NullReferenceException"/> that it then swallows: a burst of
    ///first-chance exceptions, one per row, every time. Forwarding the click here has no binding to re-evaluate.
    ///</remarks>
    private static void RunRowCommand(object? sender, ICommand command)
    {
        if (sender is BindableObject { BindingContext: AudioClip clip } && command.CanExecute(clip))
        {
            command.Execute(clip);
        }
    }

    ///<summary>
    ///Asks before deleting the tapped clip, because its audio file is deleted too and can't be brought back.
    ///</summary>
    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (sender is not BindableObject { BindingContext: AudioClip clip })
        {
            return;
        }

        bool confirmed = await DisplayAlertAsync("Delete clip?", $"'{clip.Name}' and its audio file will be permanently deleted.", "Delete", "Cancel");
        if (confirmed)
        {
            RunRowCommand(sender, _viewModel.DeleteCommand);
        }
    }

    ///<summary>
    ///Fits as many tile columns as the width allows (about 140 units each) instead of a fixed five.
    ///</summary>
    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        if (Width > 0)
        {
            TileLayout.Span = Math.Max(2, (int)(Width / 140));
        }
    }

    private void OnEditClicked(object? sender, EventArgs e) => RunRowCommand(sender, _viewModel.EditCommand);

    private void OnEditTapped(object? sender, TappedEventArgs e) => RunRowCommand(sender, _viewModel.EditCommand);

    private void OnPlayClicked(object? sender, EventArgs e) => RunRowCommand(sender, _viewModel.PlayCommand);

    private void OnPlayTapped(object? sender, TappedEventArgs e) => RunRowCommand(sender, _viewModel.PlayCommand);

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
