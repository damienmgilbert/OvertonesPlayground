using OvertonesPlayground.Themes;
using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground;

///<summary>
///The app's entry point: sets up the main window and restores the user's saved theme.
///</summary>
public partial class App : Application
{
    #region Fields
    private readonly ILogger<App> _logger;
    private readonly IServiceProvider _services;
    #endregion

    #region Constructors
    ///<summary>
    ///Initializes the application and applies the persisted light/dark/system theme preference.
    ///</summary>
    public App(IServiceProvider services, ILogger<App> logger)
    {
        InitializeComponent();
        _services = services;
        _logger = logger;
        Log_AppConstructed();
        SettingsViewModel.ApplyTheme(SettingsViewModel.LoadSavedThemePreference());

        // The Fluent tokens are a light and a dark dictionary that are swapped, so apply the right one now (before any page
        // exists) and again whenever the theme changes: the user's Light/Dark/System choice, or the system's own switch.
        FluentTheme.Apply(RequestedTheme);
        RequestedThemeChanged += (_, e) => FluentTheme.Apply(e.RequestedTheme);

        // Every page's content eases in when the page appears, and is put back at rest when it goes, so an interrupted
        // animation can never leave a page faded or offset.
        PageAppearing += (_, page) =>
        {
            if (page is ContentPage { Content: { } content })
            {
                _ = FluentMotion.EnterAsync(content);
            }
        };
        PageDisappearing += (_, page) =>
        {
            if (page is ContentPage { Content: { } content })
            {
                FluentMotion.Settle(content);
            }
        };

        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
    }
    #endregion

    #region Private methods
    private void CurrentDomain_FirstChanceException(object? sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e) => Log_FirstChanceException(e.Exception);

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) => Log_UnhandledException(e.ExceptionObject as Exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "App constructed.")]
    private partial void Log_AppConstructed();

    [LoggerMessage(Level = LogLevel.Error, Message = "First chance exception.")]
    private partial void Log_FirstChanceException(Exception? exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception occurred.")]
    private partial void Log_UnhandledException(Exception? exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Window activated.")]
    private partial void Log_WindowActivated();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Window created.")]
    private partial void Log_WindowCreated();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Window deactivated.")]
    private partial void Log_WindowDeactivated();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Window destroying.")]
    private partial void Log_WindowDestroying();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Window resumed.")]
    private partial void Log_WindowResumed();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Window stopped.")]
    private partial void Log_WindowStopped();
    #endregion

    #region Protected methods
    ///<summary>
    ///Creates the app's single window, hosting the Shell-based navigation, and logs its cross-platform lifecycle events
    ///(Created/Activated/Deactivated/Stopped/Resumed/Destroying).
    ///</summary>
    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Resolved through DI (rather than `new AppShell()`) so it can receive an ILogger<AppShell>.
        Window window = new(_services.GetRequiredService<AppShell>());

        window.Created += (_, _) => Log_WindowCreated();
        window.Activated += (_, _) => Log_WindowActivated();
        window.Deactivated += (_, _) => Log_WindowDeactivated();
        window.Stopped += (_, _) => Log_WindowStopped();
        window.Resumed += (_, _) => Log_WindowResumed();
        window.Destroying += (_, _) => Log_WindowDestroying();

        return window;
    }
    #endregion
}