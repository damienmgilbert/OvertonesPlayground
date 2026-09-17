using Microsoft.Extensions.DependencyInjection;
using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground;

/// <summary>The app's entry point: sets up the main window and restores the user's saved theme.</summary>
public partial class App : Application
{
    /// <summary>Initializes the application and applies the persisted light/dark/system theme preference.</summary>
    public App()
    {
        InitializeComponent();
        SettingsViewModel.ApplyTheme(SettingsViewModel.LoadSavedThemePreference());
    }

    /// <summary>Creates the app's single window, hosting the Shell-based navigation.</summary>
    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}