using Android.App;
using Android.Runtime;

namespace OvertonesPlayground;

/// <summary>The Android <see cref="Android.App.Application"/> subclass MAUI requires as the process entry point.</summary>
[Application]
public class MainApplication : MauiApplication
{
    /// <summary>Required constructor signature for JNI activation.</summary>
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    /// <summary>Builds the shared MAUI app instance via <see cref="MauiProgram"/>.</summary>
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
