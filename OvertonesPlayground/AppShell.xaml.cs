using OvertonesPlayground.Views;

namespace OvertonesPlayground;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("editor", typeof(AudioEditorPage));
    }
}
