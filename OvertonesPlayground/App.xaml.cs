using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground;

/// <summary>The app's entry point: sets up the main window and restores the user's saved theme.</summary>
public partial class App : Application
{
    private readonly ILogger<App> _logger;
    private readonly IServiceProvider _services;

    /// <summary>Initializes the application and applies the persisted light/dark/system theme preference.</summary>
    public App(IServiceProvider services, ILogger<App> logger)
    {
        InitializeComponent();
        _services = services;
        _logger = logger;
        Log_AppConstructed();
        SettingsViewModel.ApplyTheme(SettingsViewModel.LoadSavedThemePreference());

        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;

    }

    private void CurrentDomain_FirstChanceException(object? sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
    {
        Log_FirstChanceException(e.Exception);
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log_UnhandledException(e.ExceptionObject as Exception);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "App constructed.")]
    private partial void Log_AppConstructed();

    [LoggerMessage(Level = LogLevel.Error, Message = "First chance exception.")]
    private partial void Log_FirstChanceException(Exception? exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception occurred.")]
    private partial void Log_UnhandledException(Exception? exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Window created.")]
    private partial void Log_WindowCreated();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Window activated.")]
    private partial void Log_WindowActivated();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Window deactivated.")]
    private partial void Log_WindowDeactivated();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Window stopped.")]
    private partial void Log_WindowStopped();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Window resumed.")]
    private partial void Log_WindowResumed();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Window destroying.")]
    private partial void Log_WindowDestroying();

    /// <summary>Creates the app's single window, hosting the Shell-based navigation, and logs its cross-platform
    /// lifecycle events (Created/Activated/Deactivated/Stopped/Resumed/Destroying).</summary>
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
}