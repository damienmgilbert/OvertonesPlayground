using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;
using Plugin.Maui.Audio;

namespace OvertonesPlayground.Services.Implementations;

/// <summary>
/// Single home for every <see cref="IAudioPlayer"/> instance in the app. Keeping them all
/// behind one service means the Launchpad, Mixer, and Player screens never fight each other
/// over the same voice, and everything can be silenced together via <see cref="StopEverything"/>.
/// </summary>
public class AudioPlaybackService : IAudioPlaybackService
{
    private readonly IAudioManager _audioManager;
    private readonly IAudioFocusService _audioFocusService;
    private readonly Dictionary<int, List<IAudioPlayer>> _padVoices = [];
    private readonly Dictionary<string, IAudioPlayer> _channelPlayers = [];
    private IAudioPlayer? _mainPlayer;

    /// <summary>Creates the playback service and starts listening for audio focus changes.</summary>
    public AudioPlaybackService(IAudioManager audioManager, IAudioFocusService audioFocusService)
    {
        _audioManager = audioManager;
        _audioFocusService = audioFocusService;
        _audioFocusService.FocusChanged += OnAudioFocusChanged;
    }

    /// <summary>Pauses the main transport when focus is lost (a call, another app, etc.) rather than fighting for the speaker.</summary>
    private void OnAudioFocusChanged(object? sender, bool haveFocus)
    {
        if (!haveFocus)
        {
            Pause();
        }
    }

    /// <inheritdoc />
    public AudioClip? CurrentClip { get; private set; }

    /// <inheritdoc />
    public bool IsPlaying => _mainPlayer?.IsPlaying ?? false;

    /// <inheritdoc />
    public TimeSpan Position => TimeSpan.FromSeconds(_mainPlayer?.CurrentPosition ?? 0);

    /// <inheritdoc />
    public TimeSpan Duration => TimeSpan.FromSeconds(_mainPlayer?.Duration ?? 0);

    /// <inheritdoc />
    public double Volume
    {
        get => _mainPlayer?.Volume ?? 1.0;
        set
        {
            _mainPlayer?.Volume = value;
        }
    }

    /// <inheritdoc />
    public event EventHandler? PlaybackStateChanged;

    /// <inheritdoc />
    public event EventHandler? PlaybackEnded;

    /// <inheritdoc />
    public Task LoadAsync(AudioClip clip)
    {
        _mainPlayer?.Stop();
        _mainPlayer?.Dispose();

        _mainPlayer = _audioManager.CreatePlayer(clip.FilePath);
        _mainPlayer.PlaybackEnded += OnMainPlaybackEnded;
        CurrentClip = clip;
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Play()
    {
        _audioFocusService.RequestFocus();
        _mainPlayer?.Play();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Pause()
    {
        _mainPlayer?.Pause();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Stop()
    {
        _mainPlayer?.Stop();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Seek(TimeSpan position) => _mainPlayer?.Seek(position.TotalSeconds);

    /// <summary>Forwards the main player's own end-of-clip event as both state-changed and playback-ended.</summary>
    private void OnMainPlaybackEnded(object? sender, EventArgs e)
    {
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        PlaybackEnded?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void TriggerPad(LaunchpadPad pad)
    {
        if (!pad.HasClip)
        {
            return;
        }

        _audioFocusService.RequestFocus();
        var voice = _audioManager.CreatePlayer(pad.ClipPath!);
        voice.Volume = pad.Volume;
        voice.Loop = pad.IsLooping;

        if (!_padVoices.TryGetValue(pad.Index, out var voices))
        {
            voices = [];
            _padVoices[pad.Index] = voices;
        }

        voices.Add(voice);

        voice.PlaybackEnded += (_, _) =>
        {
            voices.Remove(voice);
            voice.Dispose();
        };

        voice.Play();
    }

    /// <inheritdoc />
    public void StopPad(int padIndex)
    {
        if (!_padVoices.TryGetValue(padIndex, out var voices))
        {
            return;
        }

        foreach (var voice in voices.ToArray())
        {
            voice.Stop();
            voice.Dispose();
        }

        voices.Clear();
    }

    /// <inheritdoc />
    public void StopAllPads()
    {
        foreach (var index in _padVoices.Keys.ToArray())
        {
            StopPad(index);
        }
    }

    /// <inheritdoc />
    public void PlayChannel(MixerChannelStrip channel)
    {
        if (string.IsNullOrEmpty(channel.SourceClipPath))
        {
            return;
        }

        StopChannel(channel.Id);

        _audioFocusService.RequestFocus();
        var player = _audioManager.CreatePlayer(channel.SourceClipPath);
        player.Loop = true;
        player.Volume = channel.IsMuted ? 0 : channel.Volume;
        player.Balance = channel.Pan;

        _channelPlayers[channel.Id] = player;
        player.Play();
    }

    /// <inheritdoc />
    public void StopChannel(string channelId)
    {
        if (_channelPlayers.Remove(channelId, out var player))
        {
            player.Stop();
            player.Dispose();
        }
    }

    /// <inheritdoc />
    public void UpdateChannel(MixerChannelStrip channel)
    {
        if (_channelPlayers.TryGetValue(channel.Id, out var player))
        {
            player.Volume = channel.IsMuted ? 0 : channel.Volume;
            player.Balance = channel.Pan;
        }
    }

    /// <inheritdoc />
    public void StopAllChannels()
    {
        foreach (var id in _channelPlayers.Keys.ToArray())
        {
            StopChannel(id);
        }
    }

    /// <inheritdoc />
    public void StopEverything()
    {
        Stop();
        StopAllPads();
        StopAllChannels();
        _audioFocusService.AbandonFocus();
    }
}
