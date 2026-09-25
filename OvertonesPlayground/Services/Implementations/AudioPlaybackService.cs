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
    ///Forgets a pad voice and frees its player. Does nothing if the voice was already released (it ended, or was stopped).
    ///</summary>
    private static void Release(List<IAudioPlayer> owner, IAudioPlayer voice)
    {
        bool wasTracked = owner.Remove(voice);
        if (wasTracked)
        {
            voice.Dispose();
        }
    }

    ///<summary>
    ///Cuts a pad voice off once it has played for <paramref name="length"/>. It carries on from the caller's thread, so the
    ///player is touched from the same thread that created it.
    ///</summary>
    private static async Task ReleaseAfterAsync(List<IAudioPlayer> owner, IAudioPlayer voice, TimeSpan length)
    {
        await Task.Delay(length);
        bool isStillPlaying = owner.Contains(voice);
        if (isStillPlaying)
        {
            voice.Stop();
            Release(owner, voice);
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
        _mainPlayer.Volume = Volume;
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
        player.Loop = channel.IsLooping;
        player.Volume = channel.AudibleVolume;
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
        _ = _channelPlayers.Remove(channelId, out IAudioPlayer? player);
        try
        {
            player?.Stop();
        }
        finally
        {
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
    public void StopPad(int voiceKey)
    {
        bool found = !_padVoices.TryGetValue(voiceKey, out List<IAudioPlayer>? voices);
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
        bool hasNoClip = !pad.HasClip;
        if (hasNoClip)
        {
            return;
        }

        TriggerVoice(pad.VoiceKey, pad.ClipPath!, new PadVoiceOptions(pad.Volume, Loop: pad.IsLooping));
    }

    ///<inheritdoc/>
    public void TriggerVoice(int voiceKey, string clipPath, PadVoiceOptions options)
    {
        _audioFocusService.RequestFocus();
        IAudioPlayer voice = _audioManager.CreatePlayer(clipPath);
        voice.Volume = Math.Clamp(options.Volume, 0, 1);
        voice.Balance = Math.Clamp(options.Balance, -1, 1);
        voice.Loop = options.Loop;

        // Speed also changes pitch. It isn't available on every player, and each has its own limits.
        bool changesSpeed = Math.Abs(options.Speed - 1) > 0.001 && voice.CanSetSpeed;
        if (changesSpeed)
        {
            voice.Speed = Math.Clamp(options.Speed, voice.MinimumSpeed, voice.MaximumSpeed);
        }

        bool isNew = !_padVoices.TryGetValue(voiceKey, out List<IAudioPlayer>? voices);
        if (isNew)
        {
            voices = [];
            _padVoices[voiceKey] = voices;
        }

        List<IAudioPlayer> owner = voices!;
        owner.Add(voice);
        voice.PlaybackEnded += (_, _) => Release(owner, voice);
        voice.Play();

        if (options.MaxLength is { } maxLength)
        {
            _ = ReleaseAfterAsync(owner, voice, maxLength);
        }
    }

    ///<inheritdoc/>
    public void UpdateChannel(MixerChannelStrip channel)
    {
        bool found = _channelPlayers.TryGetValue(channel.Id, out IAudioPlayer? player);
        if (found)
        {
            player?.Volume = channel.AudibleVolume;
            player?.Balance = channel.Pan;
            player?.Loop = channel.IsLooping;
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
    ///<remarks>
    ///The value is kept here because each loaded clip gets a new player, which would otherwise start at full volume
    ///however the user had set the slider.
    ///</remarks>
    public double Volume
    {
        get;
        set
        {
            field = value;
            _mainPlayer?.Volume = value;
        }
    } = 1.0;
    #endregion
}
