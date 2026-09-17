using Microsoft.Extensions.Logging;
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
        _logger.LogDebug("App constructed.");
        SettingsViewModel.ApplyTheme(SettingsViewModel.LoadSavedThemePreference());

        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;

    }

    private void CurrentDomain_FirstChanceException(object? sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "First chance exception.");
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        _logger.LogError(e.ExceptionObject as Exception, "Unhandled exception occurred.");
    }

    /// <summary>Creates the app's single window, hosting the Shell-based navigation, and logs its cross-platform
    /// lifecycle events (Created/Activated/Deactivated/Stopped/Resumed/Destroying).</summary>
    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Resolved through DI (rather than `new AppShell()`) so it can receive an ILogger<AppShell>.
        Window window = new(_services.GetRequiredService<AppShell>());

        window.Created += (_, _) => _logger.LogDebug("Window created.");
        window.Activated += (_, _) => _logger.LogDebug("Window activated.");
        window.Deactivated += (_, _) => _logger.LogDebug("Window deactivated.");
        window.Stopped += (_, _) => _logger.LogDebug("Window stopped.");
        window.Resumed += (_, _) => _logger.LogDebug("Window resumed.");
        window.Destroying += (_, _) => _logger.LogDebug("Window destroying.");

        return window;
    }
}