using CommunityToolkit.Mvvm.ComponentModel;

namespace OvertonesPlayground.ViewModels;

///<summary>
///Base view model that exposes common properties used by pages, such as Title, busy state and a status message.
///</summary>
public partial class BaseViewModel : ObservableObject
{
    #region Public properties
    ///<summary>
    ///Indicates whether the view model is performing work.
    ///</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    ///<summary>
    ///Optional status or error message to show to the user.
    ///</summary>
    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    ///<summary>
    ///Page title displayed in the UI.
    ///</summary>
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;
    #endregion
}
