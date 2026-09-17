using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;
using Plugin.Maui.Audio;

namespace OvertonesPlayground.Services.Implementations;

///<summary>
///Single home for every <see cref="IAudioPlayer"/> instance in the app. Keeping them all behind one service means the
///Launchpad, Mixer, and Player screens never fight each other over the same voice, and everything can be silenced
///together via <see cref="StopEverything"/>.
///</summary>
public class AudioPlaybackService : IAudioPlaybackService
{
    #region Fields
    private readonly IAudioFocusService _audioFocusService;
    private readonly IAudioManager _audioManager;
    private readonly Dictionary<string, IAudioPlayer> _channelPlayers = [];
    private IAudioPlayer? _mainPlayer;
    private readonly Dictionary<int, List<IAudioPlayer>> _padVoices = [];
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the playback service and starts listening for audio focus changes.
    ///</summary>
    public AudioPlaybackService(IAudioManager audioManager, IAudioFocusService audioFocusService)
    {
        _audioManager = audioManager;
        _audioFocusService = audioFocusService;
        _audioFocusService.FocusChanged += OnAudioFocusChanged;
    }
    #endregion

    #region Events
    ///<inheritdoc/>
    public event EventHandler? PlaybackEnded;

    ///<inheritdoc/>
    public event EventHandler? PlaybackStateChanged;
    #endregion

    #region Private methods
    ///<summary>
    ///Pauses the main transport when focus is lost (a call, another app, etc.) rather than fighting for the speaker.
    ///</summary>
    private void OnAudioFocusChanged(object? sender, bool haveFocus)
    {
        bool lostFocus = !haveFocus;
        if (lostFocus)
        {
            Pause();
        }
    }

    ///<summary>
    ///Forwards the main player's own end-of-clip event as both state-changed and playback-ended.
    ///</summary>
    private void OnMainPlaybackEnded(object? sender, EventArgs e)
    {
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
        PlaybackEnded?.Invoke(this, EventArgs.Empty);
    }
    #endregion

    #region Public methods
    ///<inheritdoc/>
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

    ///<inheritdoc/>
    public void Pause()
    {
        _mainPlayer?.Pause();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    ///<inheritdoc/>
    public void Play()
    {
        _audioFocusService.RequestFocus();
        _mainPlayer?.Play();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    ///<inheritdoc/>
    public void PlayChannel(MixerChannelStrip channel)
    {
        bool isNullOrEmpty = string.IsNullOrEmpty(channel.SourceClipPath);
        if (isNullOrEmpty)
        {
            return;
        }

        StopChannel(channel.Id);

        _audioFocusService.RequestFocus();
        IAudioPlayer player = _audioManager.CreatePlayer(channel.SourceClipPath!);
        player.Loop = true;
        player.Volume = channel.IsMuted ? 0 : channel.Volume;
        player.Balance = channel.Pan;

        _channelPlayers[channel.Id] = player;
        player.Play();
    }

    ///<inheritdoc/>
    public void Seek(TimeSpan position) => _mainPlayer?.Seek(position.TotalSeconds);

    ///<inheritdoc/>
    public void Stop()
    {
        _mainPlayer?.Stop();
        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
    }

    ///<inheritdoc/>
    public void StopAllChannels()
    {
        string[] ids = [.. _channelPlayers.Keys];
        foreach (string id in ids)
        {
            StopChannel(id);
        }
    }

    ///<inheritdoc/>
    public void StopAllPads()
    {
        int[] indices = [.. _padVoices.Keys];
        foreach (int index in indices)
        {
            StopPad(index);
        }
    }

    ///<inheritdoc/>
    public void StopChannel(string channelId)
    {
        bool removed = _channelPlayers.Remove(channelId, out IAudioPlayer? player);
        if (removed)
        {
            player?.Stop();
            player?.Dispose();
        }
    }

    ///<inheritdoc/>
    public void StopEverything()
    {
        Stop();
        StopAllPads();
        StopAllChannels();
        _audioFocusService.AbandonFocus();
    }

    ///<inheritdoc/>
    public void StopPad(int padIndex)
    {
        bool found = !_padVoices.TryGetValue(padIndex, out List<IAudioPlayer>? voices);
        if (found)
        {
            return;
        }

        IAudioPlayer[] voices2 = voices?.ToArray() ?? [];
        foreach (IAudioPlayer voice in voices2)
        {
            voice.Stop();
            voice.Dispose();
        }

        voices?.Clear();
    }

    ///<inheritdoc/>
    public void TriggerPad(LaunchpadPad pad)
    {
        bool hasClip = !pad.HasClip;
        if (hasClip)
        {
            return;
        }

        _audioFocusService.RequestFocus();
        IAudioPlayer voice = _audioManager.CreatePlayer(pad.ClipPath!);
        voice.Volume = pad.Volume;
        voice.Loop = pad.IsLooping;

        bool found = !_padVoices.TryGetValue(pad.Index, out List<IAudioPlayer>? voices);
        if (found)
        {
            voices = [];
            _padVoices[pad.Index] = voices;
        }

        voices?.Add(voice);

        voice.PlaybackEnded += (_, _) =>
        {
            voices?.Remove(voice);
            voice?.Dispose();
        };

        voice.Play();
    }

    ///<inheritdoc/>
    public void UpdateChannel(MixerChannelStrip channel)
    {
        bool found = _channelPlayers.TryGetValue(channel.Id, out IAudioPlayer? player);
        if (found)
        {
            player?.Volume = channel.IsMuted ? 0 : channel.Volume;
            player?.Balance = channel.Pan;
        }
    }
    #endregion

    #region Public properties
    ///<inheritdoc/>
    public AudioClip? CurrentClip { get; private set; }

    ///<inheritdoc/>
    public TimeSpan Duration => TimeSpan.FromSeconds(_mainPlayer?.Duration ?? 0);

    ///<inheritdoc/>
    public bool IsPlaying => _mainPlayer?.IsPlaying ?? false;

    ///<inheritdoc/>
    public TimeSpan Position => TimeSpan.FromSeconds(_mainPlayer?.CurrentPosition ?? 0);

    ///<inheritdoc/>
    public double Volume { get => _mainPlayer?.Volume ?? 1.0; set => _mainPlayer?.Volume = value; }
    #endregion
}
