using OvertonesPlayground.Views;

namespace OvertonesPlayground;

/// <summary>The app's flyout-based navigation shell, hosting every top-level page.</summary>
public partial class AppShell : Shell
{
    private readonly ILogger<AppShell> _logger;

    /// <summary>Creates the shell and registers the Sound Editor's detail route (pushed on top of Library, not a flyout item).</summary>
    public AppShell(ILogger<AppShell> logger)
    {
        InitializeComponent();
        _logger = logger;

        Routing.RegisterRoute("editor", typeof(AudioEditorPage));
        Routing.RegisterRoute("trim", typeof(TrimPage));


        Navigating += (_, e) => Log_Navigating(e.Current?.Location, e.Target?.Location);
        Navigated += (_, e) => Log_Navigated(e.Current?.Location, e.Source);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Navigating from '{Current}' to '{Target}'.")]
    private partial void Log_Navigating(Uri? current, Uri? target);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Navigated to '{Current}' ({Source}).")]
    private partial void Log_Navigated(Uri? current, ShellNavigationSource source);
}
