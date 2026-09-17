using OvertonesPlayground.Views;

namespace OvertonesPlayground;

/// <summary>The app's flyout-based navigation shell, hosting every top-level page.</summary>
public partial class AppShell : Shell
{
    /// <summary>Creates the shell and registers the Sound Editor's detail route (pushed on top of Library, not a flyout item).</summary>
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("editor", typeof(AudioEditorPage));
    }
}
