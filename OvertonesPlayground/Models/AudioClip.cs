namespace OvertonesPlayground.Models;

public class AudioClip
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public TimeSpan Duration { get; set; }

    public DateTimeOffset ImportedAt { get; init; } = DateTimeOffset.Now;

    public bool IsUserRecording { get; set; }
}
