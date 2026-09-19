namespace OvertonesPlayground.Tests.TestSupport;

/// <summary>
/// Small builders for the model objects tests keep needing.
/// </summary>
internal static class TestData
{
    public static AudioClip Clip(string name = "Clip", double seconds = 10, string? path = null, string? publicLocation = null) => new()
    {
        Name = name,
        FilePath = path ?? $"/audio/{name}.wav",
        Duration = TimeSpan.FromSeconds(seconds),
        PublicStorageLocation = publicLocation,
    };

    public static IReadOnlyList<AudioClip> Clips(params AudioClip[] clips) => clips;
}
