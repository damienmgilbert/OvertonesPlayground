namespace OvertonesPlayground.Models;

public class AudioClip
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public TimeSpan Duration { get; set; }

    public DateTimeOffset ImportedAt { get; init; } = DateTimeOffset.Now;

    public bool IsUserRecording { get; set; }

    /// <summary>Where this clip was also copied to in shared storage, e.g. "Music/OvertonesPlayground/kick.wav" - null if it hasn't been exported.</summary>
    public string? PublicStorageLocation { get; set; }
}
