using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

///<summary>
///Code-behind for the Library page: loads the clip catalog each time the page appears.
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
    }

    ///<summary>
    ///Logs when the page is no longer visible.
    ///</summary>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Log_PageDisappeared();
    }
    #endregion
}
