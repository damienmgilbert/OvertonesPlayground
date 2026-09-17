using Microsoft.Extensions.Logging;
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

        Navigating += (_, e) => _logger.LogDebug("Navigating from '{Current}' to '{Target}'.", e.Current?.Location, e.Target?.Location);
        Navigated += (_, e) => _logger.LogDebug("Navigated to '{Current}' ({Source}).", e.Current?.Location, e.Source);
    }
}
