using Android.App;
using Android.Content.PM;

namespace OvertonesPlayground;

/// <summary>
/// The app's single Android activity. <c>ConfigChanges.UiMode</c> is required so toggling
/// light/dark mode (in-app or system-wide) updates the running app instead of restarting it.
/// </summary>
[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}
