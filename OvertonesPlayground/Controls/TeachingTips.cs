namespace OvertonesPlayground.Controls;

/// <summary>
/// Remembers which one-time <see cref="TeachingPopover"/> tips the user has already seen, so each is shown once and can be
/// brought back from Settings. Stored in <see cref="Preferences"/>: it is a handful of flags, not user content.
/// </summary>
public static class TeachingTips
{
    #region Constants
    private const string IndexKey = "TeachingTips.Keys";
    private const char Separator = ',';
    private const string SeenPrefix = "TeachingTips.Seen.";
    #endregion

    #region Private methods
    private static string[] ReadIndex() => Preferences.Default.Get(IndexKey, string.Empty).Split(Separator, StringSplitOptions.RemoveEmptyEntries);
    #endregion

    #region Public methods
    /// <summary>
    /// Whether the tip with this key has been shown and dismissed before.
    /// </summary>
    public static bool HasSeen(string key) => Preferences.Default.Get(SeenPrefix + key, false);

    /// <summary>
    /// Records that the tip with this key has been shown and dismissed.
    /// </summary>
    public static void MarkSeen(string key)
    {
        Preferences.Default.Set(SeenPrefix + key, true);

        // Preferences can't be enumerated, so the keys are also kept in one list for ResetAll to find.
        HashSet<string> keys = [.. ReadIndex(), key];
        Preferences.Default.Set(IndexKey, string.Join(Separator, keys));
    }

    /// <summary>
    /// Forgets every tip that has been seen, so each shows again the next time its page opens.
    /// </summary>
    public static void ResetAll()
    {
        foreach (string key in ReadIndex())
        {
            Preferences.Default.Remove(SeenPrefix + key);
        }

        Preferences.Default.Remove(IndexKey);
    }
    #endregion
}
