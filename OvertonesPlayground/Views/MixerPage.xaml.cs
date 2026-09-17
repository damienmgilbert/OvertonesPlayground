using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

///<summary>
///Code-behind for the Mixer page; all behavior lives in <see cref="MixerViewModel"/>.
///</summary>
public partial class MixerPage : ContentPage
{
    #region Fields
    private readonly ILogger<MixerPage> _logger;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the page and binds it to its view model.
    ///</summary>
    public MixerPage(MixerViewModel viewModel, ILogger<MixerPage> logger)
    {
        InitializeComponent();
        BindingContext = viewModel;
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
