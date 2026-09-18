using Android.Views;
using AndroidX.Core.View;
using Google.Android.Material.AppBar;
using Microsoft.Maui.Platform;
using AColor = Android.Graphics.Color;
using Activity = Android.App.Activity;
using Color = Microsoft.Maui.Graphics.Color;
using View = Android.Views.View;
using Window = Android.Views.Window;

namespace OvertonesPlayground;

/// <summary>
/// Makes the Android window edge-to-edge: the system bars are transparent with no automatic contrast scrim, the Shell
/// app bar's color continues up behind the status bar, and the bar icons flip between light and dark to stay legible on
/// whatever surface (light or dark theme) sits behind them.
/// </summary>
/// <remarks>
/// From Android 15 the OS already draws behind the system bars, but the Material <c>AppBarLayout</c> that hosts the Shell
/// toolbar starts at the top of the window while the toolbar inside it is inset below the status bar. MAUI colors only
/// the toolbar, so the strip above it showed the theme's <c>colorPrimary</c> (a purple band). It has to be recolored in
/// code, not through a theme resource, because the activity handles <c>UiMode</c> changes itself: a light/dark switch
/// never re-inflates the view, so a resource-based color would go stale until the next launch.
/// </remarks>
internal static class SystemBars
{
    #region Fields
    private static bool _isAttached;
    #endregion

    #region Private methods
    ///<summary>
    ///Applies the bar-colored app bar and icon contrast for the current page and theme.
    ///</summary>
    private static void Apply()
    {
        Activity? activity = Platform.CurrentActivity;
        Window? window = activity?.Window;
        Shell? shell = Shell.Current;
        if (window is null || shell is null)
        {
            return;
        }

        Color? barColor = Shell.GetBackgroundColor(shell);
        Color? pageColor = shell.CurrentPage?.BackgroundColor ?? barColor;

        View decorView = window.DecorView;
        if (barColor is not null)
        {
            ColorAppBars(decorView, barColor.ToPlatform());
        }

        // "Light" appearance means dark icons, for a light surface. The status bar sits on the app bar; the navigation bar
        // sits on the page's background.
        WindowInsetsControllerCompat? controller = WindowCompat.GetInsetsController(window, decorView);
        if (controller is not null)
        {
            controller.AppearanceLightStatusBars = IsLight(barColor);
            controller.AppearanceLightNavigationBars = IsLight(pageColor);
        }
    }

    ///<summary>
    ///Colors every app bar in the activity's view tree. There can be more than one: a page pushed onto the navigation stack
    ///(such as Trim or the Audio Editor) gets its own, separate from the one behind it.
    ///</summary>
    private static void ColorAppBars(View? view, AColor color)
    {
        if (view is AppBarLayout appBar)
        {
            appBar.SetBackgroundColor(color);
            return;
        }

        if (view is ViewGroup group)
        {
            for (int i = 0; i < group.ChildCount; i++)
            {
                ColorAppBars(group.GetChildAt(i), color);
            }
        }
    }

    ///<summary>
    ///Whether a surface is light enough to need dark icons. With no color to go on, follows the app theme.
    ///</summary>
    private static bool IsLight(Color? surface) => surface is not null ? surface.GetLuminosity() >= 0.5f : Application.Current?.RequestedTheme != AppTheme.Dark;
    #endregion

    #region Public methods
    ///<summary>
    ///Hooks the shell so the bars are re-applied after every navigation (each page can have its own colors) and every
    ///light/dark switch. Safe to call more than once.
    ///</summary>
    public static void Attach(Shell shell)
    {
        if (_isAttached)
        {
            return;
        }

        _isAttached = true;
        shell.Navigated += (_, _) => Refresh();
        if (Application.Current is { } app)
        {
            app.RequestedThemeChanged += (_, _) => Refresh();
        }

        Refresh();
    }

    ///<summary>
    ///One-time window setup: transparent system bars with no automatic contrast scrim behind the navigation bar.
    ///</summary>
    public static void ConfigureWindow(Activity activity)
    {
        Window? window = activity.Window;
        if (window is null)
        {
            return;
        }

        // From API 35 the bars are always transparent and these setters are no-ops; older versions need them.
        if (!OperatingSystem.IsAndroidVersionAtLeast(35))
        {
            window.SetStatusBarColor(AColor.Transparent);
            window.SetNavigationBarColor(AColor.Transparent);
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            // Otherwise the system darkens the strip behind a 3-button navigation bar, which shows as a black band.
            window.NavigationBarContrastEnforced = false;
        }
    }

    ///<summary>
    ///Re-applies the bars once MAUI has finished applying its own themed colors for the change that triggered this.
    ///</summary>
    public static void Refresh() => Application.Current?.Dispatcher.Dispatch(Apply);
    #endregion
}
