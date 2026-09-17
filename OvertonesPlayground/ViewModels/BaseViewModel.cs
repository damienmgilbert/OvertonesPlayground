using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace OvertonesPlayground.ViewModels;

///<summary>
///Base view model that exposes common properties used by pages, such as Title, busy state and a status message, plus a
///<see cref="Logger"/> every derived view model uses to trace its commands during debugging.
///</summary>
public partial class BaseViewModel : ObservableObject
{
    #region Constructors
    protected BaseViewModel(ILogger logger)
    {
        _logger = logger;
        _logger.LogDebug("{ViewModel} created.", GetType().Name);
    }
    #endregion

    #region Protected properties
    ///<summary>
    ///Logger scoped to the concrete view model type, used to trace command execution during debugging.
    ///</summary>
    protected ILogger _logger;
    #endregion

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
