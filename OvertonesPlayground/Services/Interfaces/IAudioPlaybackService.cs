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

    /// <summary>The clip currently loaded into the main transport, if any.</summary>
    AudioClip? CurrentClip { get; }

    /// <summary>Whether the main transport is currently playing.</summary>
    bool IsPlaying { get; }

    /// <summary>Current playback position of the main transport.</summary>
    TimeSpan Position { get; }

    /// <summary>Duration of the currently loaded clip.</summary>
    TimeSpan Duration { get; }

    /// <summary>Main transport volume, from 0 (silent) to 1 (full scale).</summary>
    double Volume { get; set; }

    /// <summary>Raised whenever the main transport starts, pauses, stops, or finishes a clip.</summary>
    event EventHandler? PlaybackStateChanged;

    /// <summary>Raised when the main transport's clip finishes playing on its own.</summary>
    event EventHandler? PlaybackEnded;

    /// <summary>Loads a clip into the main transport, replacing whatever was previously loaded.</summary>
    Task LoadAsync(AudioClip clip);

    /// <summary>Begins or resumes playback on the main transport.</summary>
    void Play();

    /// <summary>Pauses the main transport without resetting its position.</summary>
    void Pause();

    /// <summary>Stops the main transport and resets its position to the start.</summary>
    void Stop();

    /// <summary>Moves the main transport's playback position.</summary>
    void Seek(TimeSpan position);

    // Launchpad one-shots

    /// <summary>Fires a new, independent voice for the given pad (multiple triggers can overlap).</summary>
    void TriggerPad(LaunchpadPad pad);

    /// <summary>Stops every currently-sounding voice for the given pad.</summary>
    void StopPad(int padIndex);

    /// <summary>Stops every currently-sounding pad voice across the whole Launchpad.</summary>
    void StopAllPads();

    // Mixer channel strips

    /// <summary>Starts (or restarts) a channel's looping voice from its assigned source.</summary>
    void PlayChannel(MixerChannelStrip channel);

    /// <summary>Stops a single channel's voice.</summary>
    void StopChannel(string channelId);

    /// <summary>Applies a channel's current volume/pan/mute settings to its live voice, without restarting it.</summary>
    void UpdateChannel(MixerChannelStrip channel);

    /// <summary>Stops every channel's voice.</summary>
    void StopAllChannels();

    /// <summary>Stops the main transport, every pad voice, and every channel voice.</summary>
    void StopEverything();
}
