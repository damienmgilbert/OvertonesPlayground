using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

///<summary>
///Code-behind for the Audio Recorder page; all behavior lives in <see cref="SoundCreatorViewModel"/>.
///</summary>
public partial class SoundCreatorPage : ContentPage
{
    #region Constructors
    ///<summary>
    ///Creates the page and binds it to its view model.
    ///</summary>
    public SoundCreatorPage(SoundCreatorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
    #endregion
}
