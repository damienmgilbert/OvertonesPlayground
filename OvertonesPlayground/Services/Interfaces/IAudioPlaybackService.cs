using OvertonesPlayground.Models;

namespace OvertonesPlayground.Services.Interfaces;

/// <summary>
/// Owns every sounding voice in the app: the single "now playing" transport used by the
/// Player page, one-shot polyphonic triggers used by the Launchpad, and the sustained,
/// live-mixed voices used by the Mixer.
/// </summary>
public interface IAudioPlaybackService
{
    // Main transport (Player page)
    AudioClip? CurrentClip { get; }
    bool IsPlaying { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    double Volume { get; set; }

    event EventHandler? PlaybackStateChanged;
    event EventHandler? PlaybackEnded;

    Task LoadAsync(AudioClip clip);
    void Play();
    void Pause();
    void Stop();
    void Seek(TimeSpan position);

    // Launchpad one-shots
    void TriggerPad(LaunchpadPad pad);
    void StopPad(int padIndex);
    void StopAllPads();

    // Mixer channel strips
    void PlayChannel(MixerChannelStrip channel);
    void StopChannel(string channelId);
    void UpdateChannel(MixerChannelStrip channel);
    void StopAllChannels();

    void StopEverything();
}
