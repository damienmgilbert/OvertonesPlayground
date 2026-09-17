using OvertonesPlayground.Models;

namespace OvertonesPlayground.Services.Interfaces;

public interface IAudioRecorderService
{
    bool IsRecording { get; }

    TimeSpan Elapsed { get; }

    event EventHandler<TimeSpan>? ElapsedChanged;

    Task StartAsync();

    /// <summary>Stops recording and returns a clip pointing at the captured file (not yet added to the library).</summary>
    Task<AudioClip> StopAsync(string name);

    Task CancelAsync();
}
