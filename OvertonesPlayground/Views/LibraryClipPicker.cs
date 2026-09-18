using OvertonesPlayground.Models;

namespace OvertonesPlayground.Views;

///<summary>
///Shared "pick one clip from the library" prompt, backed by <see cref="Page.DisplayActionSheetAsync(string, string, string, string[])"/>.
///Used by the Multi-Track page (add a clip to a track) and the Trim page (insert a clip at the playhead).
///</summary>
internal static class LibraryClipPicker
{
    #region Public methods
    ///<summary>
    ///Shows an action sheet listing <paramref name="clips"/> and returns the one the user picked, or null if they
    ///canceled, dismissed it, or the library is empty.
    ///</summary>
    public static async Task<AudioClip?> PickAsync(Page page, IReadOnlyList<AudioClip> clips, string title)
    {
        if (clips.Count == 0)
        {
            await page.DisplayAlertAsync(title, "Your library is empty - import or record a clip first.", "OK");
            return null;
        }

        Dictionary<string, AudioClip> clipsByLabel = [];
        List<string> labels = [];
        foreach (AudioClip clip in clips)
        {
            string label = $"{clip.Name} ({clip.Duration:mm\\:ss})";
            string uniqueLabel = label;
            int suffix = 2;
            while (!clipsByLabel.TryAdd(uniqueLabel, clip))
            {
                uniqueLabel = $"{label} #{suffix++}";
            }

            labels.Add(uniqueLabel);
        }

        string? chosen = await page.DisplayActionSheetAsync(title, "Cancel", null, [.. labels]);
        return chosen is not null && clipsByLabel.TryGetValue(chosen, out AudioClip? match) ? match : null;
    }
    #endregion
}
