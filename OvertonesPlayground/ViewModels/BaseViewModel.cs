using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OvertonesPlayground.ViewModels;

///<summary>
///Base view model that exposes common properties used by pages, such as Title, busy state and a status message, plus a
///</summary>
public partial class BaseViewModel : ObservableObject
{
    #region Fields

    ///<summary>
    ///Logger scoped to the concrete view model type, used to trace command execution during debugging. This is a
    ///protected field rather than a property because the [LoggerMessage] source generator requires an accessible
    ///ILogger field in every derived view model that declares generated log methods (SYSLIB1019 otherwise).
    ///</summary>
    [SuppressMessage("Design", "CA1051:Do not declare visible instance fields", Justification = "The [LoggerMessage] source generator requires an accessible ILogger field, not a property, in derived view models.")]
    protected readonly ILogger _logger;
    #endregion

    #region Constructors
    protected BaseViewModel(ILogger logger)
    {
        _logger = logger;
        Log_ViewModelCreated(GetType().Name);
    }
    #endregion

    #region Private methods
    [LoggerMessage(Level = LogLevel.Debug, Message = "{ViewModel} created.")]
    private partial void Log_ViewModelCreated(string viewModel);
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
