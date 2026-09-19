using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Controls;

/// <summary>
/// Remembers which one-time <see cref="TeachingPopover"/> tips the user has already seen, so each is shown once and can be
/// brought back from Settings. A static way in for the popover, which is a control and so is not built through dependency
/// injection; the logic lives in <see cref="TeachingTipsService"/>, which view models use through
/// <see cref="Services.Interfaces.ITeachingTipsService"/> and which shares this class's <see cref="Preferences"/>.
/// </summary>
public static class TeachingTips
{
    #region Fields
    private static readonly TeachingTipsService Service = new(Preferences.Default);
    #endregion

    #region Public methods
    /// <summary>
    /// Whether the tip with this key has been shown and dismissed before.
    /// </summary>
    public static bool HasSeen(string key) => Service.HasSeen(key);

    /// <summary>
    /// Records that the tip with this key has been shown and dismissed.
    /// </summary>
    public static void MarkSeen(string key) => Service.MarkSeen(key);

    /// <summary>
    /// Forgets every tip that has been seen, so each shows again the next time its page opens.
    /// </summary>
    public static void ResetAll() => Service.ResetAll();
    #endregion
}
