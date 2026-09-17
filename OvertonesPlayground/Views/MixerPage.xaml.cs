using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

///<summary>
///Code-behind for the Mixer page; all behavior lives in <see cref="MixerViewModel"/>.
///</summary>
public partial class MixerPage : ContentPage
{
    #region Constructors
    ///<summary>
    ///Creates the page and binds it to its view model.
    ///</summary>
    public MixerPage(MixerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
    #endregion
}
