using OvertonesPlayground.Models;
using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

///<summary>
///Code-behind for the Multi-Track page: hosts the "Add Clip" library picker, which needs a native action sheet and
///so can't live in the view model.
///</summary>
public partial class MultiTrackPage : ContentPage
{
    #region Fields
    private readonly ILogger<MultiTrackPage> _logger;
    private readonly MultiTrackViewModel _viewModel;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the page and binds it to its view model.
    ///</summary>
    public MultiTrackPage(MultiTrackViewModel viewModel, ILogger<MultiTrackPage> logger)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _logger = logger;
    }
    #endregion

    #region Private methods
    ///<summary>
    ///Shows the library clip picker and adds the chosen clip to the track the tapped button belongs to.
    ///</summary>
    private async void OnAddClipClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: TrackViewModel track })
        {
            return;
        }

        IReadOnlyList<AudioClip> clips = await _viewModel.GetLibraryClipsAsync();
        AudioClip? chosen = await LibraryClipPicker.PickAsync(this, clips, $"Add clip to {track.Name}");
        if (chosen is not null)
        {
            track.AddClip(chosen);
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

    #region Logging
    [LoggerMessage(Level = LogLevel.Debug, Message = "Page appeared.")]
    private partial void Log_PageAppeared();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Page disappeared.")]
    private partial void Log_PageDisappeared();
    #endregion
}
