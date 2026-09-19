using OvertonesPlayground.Services.Implementations;
using Plugin.Maui.Audio;

namespace OvertonesPlayground.Tests.Services;

public sealed class AudioPlaybackServiceTests
{
    private readonly IAudioFocusService _focus = Substitute.For<IAudioFocusService>();
    private readonly IAudioManager _audioManager = Substitute.For<IAudioManager>();

    /// <summary>
    /// Every player the service has asked the (fake) audio manager for, in order, keyed by the file it was created for.
    /// </summary>
    private readonly List<(string Path, IAudioPlayer Player)> _players = [];

    private AudioPlaybackService Create()
    {
        _audioManager.CreatePlayer(Arg.Any<string>()).Returns(call =>
        {
            IAudioPlayer player = Substitute.For<IAudioPlayer>();
            player.CanSetSpeed.Returns(true);
            player.MinimumSpeed.Returns(0.5);
            player.MaximumSpeed.Returns(2.0);
            _players.Add((call.Arg<string>(), player));
            return player;
        });
        return new AudioPlaybackService(_audioManager, _focus);
    }

    private IAudioPlayer PlayerFor(string path) => _players.Last(p => p.Path == path).Player;

    private IAudioPlayer LastPlayer => _players[^1].Player;

    #region The main transport
    [Fact]
    public async Task Load_CreatesAPlayerForTheClipAndRemembersIt()
    {
        AudioPlaybackService service = Create();
        AudioClip clip = TestData.Clip("Song", path: "/audio/song.wav");

        await service.LoadAsync(clip);

        Assert.Same(clip, service.CurrentClip);
        Assert.Equal("/audio/song.wav", Assert.Single(_players).Path);
    }

    [Fact]
    public async Task Load_ReportsThatThePlaybackStateChanged()
    {
        AudioPlaybackService service = Create();
        int changes = 0;
        service.PlaybackStateChanged += (_, _) => changes++;

        await service.LoadAsync(TestData.Clip("Song"));

        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task Load_AnotherClip_StopsAndReleasesTheOldPlayer()
    {
        AudioPlaybackService service = Create();
        await service.LoadAsync(TestData.Clip("One", path: "/one.wav"));
        IAudioPlayer old = LastPlayer;

        await service.LoadAsync(TestData.Clip("Two", path: "/two.wav"));

        old.Received(1).Stop();
        old.Received(1).Dispose();
        Assert.Equal("Two", service.CurrentClip!.Name);
    }

    [Fact]
    public async Task Play_TakesAudioFocusThenPlays()
    {
        AudioPlaybackService service = Create();
        await service.LoadAsync(TestData.Clip("Song"));

        service.Play();

        Received.InOrder(() =>
        {
            _focus.RequestFocus();
            LastPlayer.Play();
        });
    }

    [Fact]
    public void Play_ReportsThatThePlaybackStateChanged()
    {
        AudioPlaybackService service = Create();
        int changes = 0;
        service.PlaybackStateChanged += (_, _) => changes++;

        service.Play();

        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task PauseAndStop_PassThroughAndReportAChange()
    {
        AudioPlaybackService service = Create();
        await service.LoadAsync(TestData.Clip("Song"));
        int changes = 0;
        service.PlaybackStateChanged += (_, _) => changes++;

        service.Pause();
        service.Stop();

        LastPlayer.Received(1).Pause();
        LastPlayer.Received(1).Stop();
        Assert.Equal(2, changes);
    }

    [Fact]
    public async Task Seek_MovesThePlayerToThatManySeconds()
    {
        AudioPlaybackService service = Create();
        await service.LoadAsync(TestData.Clip("Song"));

        service.Seek(TimeSpan.FromSeconds(12.5));

        LastPlayer.Received(1).Seek(12.5);
    }

    [Fact]
    public void TransportCommands_WithNothingLoaded_AreHarmless()
    {
        AudioPlaybackService service = Create();

        service.Pause();
        service.Stop();
        service.Seek(TimeSpan.FromSeconds(3));

        Assert.Null(service.CurrentClip);
        Assert.False(service.IsPlaying);
        Assert.Equal(TimeSpan.Zero, service.Duration);
        Assert.Equal(TimeSpan.Zero, service.Position);
    }

    [Fact]
    public async Task IsPlayingDurationAndPosition_ComeFromThePlayer()
    {
        AudioPlaybackService service = Create();
        await service.LoadAsync(TestData.Clip("Song"));
        LastPlayer.IsPlaying.Returns(true);
        LastPlayer.Duration.Returns(90.0);
        LastPlayer.CurrentPosition.Returns(12.0);

        Assert.True(service.IsPlaying);
        Assert.Equal(TimeSpan.FromSeconds(90), service.Duration);
        Assert.Equal(TimeSpan.FromSeconds(12), service.Position);
    }

    [Fact]
    public async Task ClipFinishingOnItsOwn_RaisesBothEvents()
    {
        AudioPlaybackService service = Create();
        await service.LoadAsync(TestData.Clip("Song"));
        int ended = 0;
        int changed = 0;
        service.PlaybackEnded += (_, _) => ended++;
        service.PlaybackStateChanged += (_, _) => changed++;

        LastPlayer.PlaybackEnded += Raise.Event();

        Assert.Equal(1, ended);
        Assert.Equal(1, changed);
    }

    [Fact]
    public async Task LosingAudioFocus_PausesThePlayer()
    {
        AudioPlaybackService service = Create();
        await service.LoadAsync(TestData.Clip("Song"));

        _focus.FocusChanged += Raise.Event<EventHandler<bool>>(_focus, false);

        LastPlayer.Received(1).Pause();
    }

    [Fact]
    public async Task GainingAudioFocus_DoesNotStartAnything()
    {
        AudioPlaybackService service = Create();
        await service.LoadAsync(TestData.Clip("Song"));

        _focus.FocusChanged += Raise.Event<EventHandler<bool>>(_focus, true);

        LastPlayer.DidNotReceive().Pause();
        LastPlayer.DidNotReceive().Play();
    }
    #endregion

    #region Volume
    [Fact]
    public async Task Volume_SetWhileAClipIsLoaded_GoesToThePlayer()
    {
        AudioPlaybackService service = Create();
        await service.LoadAsync(TestData.Clip("Song"));

        service.Volume = 0.3;

        Assert.Equal(0.3, LastPlayer.Volume);
        Assert.Equal(0.3, service.Volume);
    }

    [Fact]
    public void Volume_NothingLoaded_IsFull()
    {
        Assert.Equal(1.0, Create().Volume);
    }

    [Fact]
    public async Task Volume_ChosenBeforeAClipIsLoaded_AppliesToIt()
    {
        AudioPlaybackService service = Create();
        service.Volume = 0.3;

        await service.LoadAsync(TestData.Clip("Song"));

        Assert.Equal(0.3, LastPlayer.Volume);
    }

    [Fact]
    public async Task Volume_ChosenForOneClip_StillAppliesWhenAnotherIsLoaded()
    {
        // The player page keeps showing the volume the user picked, so a new clip must not quietly go back to full volume.
        AudioPlaybackService service = Create();
        await service.LoadAsync(TestData.Clip("One", path: "/one.wav"));
        service.Volume = 0.3;

        await service.LoadAsync(TestData.Clip("Two", path: "/two.wav"));

        Assert.Equal(0.3, PlayerFor("/two.wav").Volume);
        Assert.Equal(0.3, service.Volume);
    }
    #endregion

    #region Mixer channels
    private static MixerChannelStrip Strip(string? source = "/loop.wav", double volume = 0.8, double pan = 0, bool muted = false) =>
        new() { SourceClipPath = source, Volume = volume, Pan = pan, IsMuted = muted };

    [Fact]
    public void PlayChannel_StartsALoopingVoiceAtTheStripsSettings()
    {
        AudioPlaybackService service = Create();

        service.PlayChannel(Strip(volume: 0.6, pan: -0.4));

        IAudioPlayer player = LastPlayer;
        Assert.True(player.Loop);
        Assert.Equal(0.6, player.Volume);
        Assert.Equal(-0.4, player.Balance);
        player.Received(1).Play();
        _focus.Received(1).RequestFocus();
    }

    [Fact]
    public void PlayChannel_MutedStrip_StartsSilent()
    {
        AudioPlaybackService service = Create();

        service.PlayChannel(Strip(volume: 0.9, muted: true));

        Assert.Equal(0, LastPlayer.Volume);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void PlayChannel_NoSampleAssigned_DoesNothing(string? source)
    {
        AudioPlaybackService service = Create();

        service.PlayChannel(Strip(source));

        Assert.Empty(_players);
        _focus.DidNotReceive().RequestFocus();
    }

    [Fact]
    public void PlayChannel_ThatIsAlreadySounding_ReplacesItsVoice()
    {
        AudioPlaybackService service = Create();
        MixerChannelStrip strip = Strip();
        service.PlayChannel(strip);
        IAudioPlayer first = LastPlayer;

        service.PlayChannel(strip);

        first.Received(1).Stop();
        first.Received(1).Dispose();
        Assert.Equal(2, _players.Count);
    }

    [Fact]
    public void UpdateChannel_ChangesTheLiveVoiceWithoutRestartingIt()
    {
        AudioPlaybackService service = Create();
        MixerChannelStrip strip = Strip(volume: 0.8);
        service.PlayChannel(strip);
        IAudioPlayer player = LastPlayer;

        strip.Volume = 0.2;
        strip.Pan = 0.5;
        service.UpdateChannel(strip);

        Assert.Equal(0.2, player.Volume);
        Assert.Equal(0.5, player.Balance);
        player.Received(1).Play();
    }

    [Fact]
    public void UpdateChannel_MutingSilencesTheLiveVoice()
    {
        AudioPlaybackService service = Create();
        MixerChannelStrip strip = Strip(volume: 0.8);
        service.PlayChannel(strip);

        strip.IsMuted = true;
        service.UpdateChannel(strip);

        Assert.Equal(0, LastPlayer.Volume);
    }

    [Fact]
    public void UpdateChannel_ChannelNotSounding_DoesNothing()
    {
        Create().UpdateChannel(Strip());

        Assert.Empty(_players);
    }

    [Fact]
    public void StopChannel_StopsAndReleasesThatVoiceOnly()
    {
        AudioPlaybackService service = Create();
        MixerChannelStrip a = Strip("/a.wav");
        MixerChannelStrip b = Strip("/b.wav");
        service.PlayChannel(a);
        service.PlayChannel(b);

        service.StopChannel(a.Id);

        PlayerFor("/a.wav").Received(1).Stop();
        PlayerFor("/a.wav").Received(1).Dispose();
        PlayerFor("/b.wav").DidNotReceive().Stop();
    }

    [Fact]
    public void StopChannel_Unknown_IsHarmless()
    {
        Create().StopChannel("nope");
    }

    [Fact]
    public void StopAllChannels_StopsEveryVoice()
    {
        AudioPlaybackService service = Create();
        service.PlayChannel(Strip("/a.wav"));
        service.PlayChannel(Strip("/b.wav"));

        service.StopAllChannels();

        PlayerFor("/a.wav").Received(1).Stop();
        PlayerFor("/b.wav").Received(1).Stop();
    }
    #endregion

    #region Pad voices
    [Fact]
    public void TriggerVoice_StartsAPlayerWithTheOptions()
    {
        AudioPlaybackService service = Create();

        service.TriggerVoice(5, "/kick.wav", new PadVoiceOptions(Volume: 0.7, Balance: -0.25, Speed: 1, Loop: true));

        IAudioPlayer voice = PlayerFor("/kick.wav");
        Assert.Equal(0.7, voice.Volume);
        Assert.Equal(-0.25, voice.Balance);
        Assert.True(voice.Loop);
        voice.Received(1).Play();
        _focus.Received(1).RequestFocus();
    }

    [Fact]
    public void TriggerVoice_VolumeAndBalanceOutsideTheirRanges_AreClamped()
    {
        AudioPlaybackService service = Create();

        service.TriggerVoice(1, "/loud.wav", new PadVoiceOptions(Volume: 3, Balance: -7));
        service.TriggerVoice(2, "/quiet.wav", new PadVoiceOptions(Volume: -1, Balance: 9));

        Assert.Equal(1, PlayerFor("/loud.wav").Volume);
        Assert.Equal(-1, PlayerFor("/loud.wav").Balance);
        Assert.Equal(0, PlayerFor("/quiet.wav").Volume);
        Assert.Equal(1, PlayerFor("/quiet.wav").Balance);
    }

    [Fact]
    public void TriggerVoice_ChangedSpeed_IsAppliedWithinThePlayersLimits()
    {
        AudioPlaybackService service = Create();

        service.TriggerVoice(1, "/fast.wav", new PadVoiceOptions(Speed: 1.5));
        service.TriggerVoice(2, "/toofast.wav", new PadVoiceOptions(Speed: 9));
        service.TriggerVoice(3, "/tooslow.wav", new PadVoiceOptions(Speed: 0.1));

        Assert.Equal(1.5, PlayerFor("/fast.wav").Speed);
        Assert.Equal(2.0, PlayerFor("/toofast.wav").Speed);
        Assert.Equal(0.5, PlayerFor("/tooslow.wav").Speed);
    }

    [Fact]
    public void TriggerVoice_NormalSpeed_LeavesTheSpeedAlone()
    {
        AudioPlaybackService service = Create();

        service.TriggerVoice(1, "/normal.wav", new PadVoiceOptions(Speed: 1.0004));

        PlayerFor("/normal.wav").DidNotReceive().Speed = Arg.Any<double>();
    }

    [Fact]
    public void TriggerVoice_PlayerCantChangeSpeed_LeavesTheSpeedAlone()
    {
        AudioPlaybackService service = Create();
        _audioManager.CreatePlayer(Arg.Any<string>()).Returns(call =>
        {
            IAudioPlayer player = Substitute.For<IAudioPlayer>();
            player.CanSetSpeed.Returns(false);
            _players.Add((call.Arg<string>(), player));
            return player;
        });

        service.TriggerVoice(1, "/fixed.wav", new PadVoiceOptions(Speed: 1.5));

        PlayerFor("/fixed.wav").DidNotReceive().Speed = Arg.Any<double>();
    }

    [Fact]
    public void TriggerVoice_TheSamePadTwice_OverlapsTwoVoices()
    {
        AudioPlaybackService service = Create();

        service.TriggerVoice(3, "/hit.wav", new PadVoiceOptions());
        service.TriggerVoice(3, "/hit.wav", new PadVoiceOptions());

        Assert.Equal(2, _players.Count);
        Assert.All(_players, entry => entry.Player.Received(1).Play());
    }

    [Fact]
    public void StopPad_SilencesEveryVoiceOfThatPadAndNoOthers()
    {
        AudioPlaybackService service = Create();
        service.TriggerVoice(3, "/a.wav", new PadVoiceOptions());
        IAudioPlayer firstOfPad3 = LastPlayer;
        service.TriggerVoice(3, "/a.wav", new PadVoiceOptions());
        IAudioPlayer secondOfPad3 = LastPlayer;
        service.TriggerVoice(4, "/b.wav", new PadVoiceOptions());
        IAudioPlayer pad4 = LastPlayer;

        service.StopPad(3);

        firstOfPad3.Received(1).Stop();
        firstOfPad3.Received(1).Dispose();
        secondOfPad3.Received(1).Stop();
        pad4.DidNotReceive().Stop();
    }

    [Fact]
    public void StopPad_NothingSounding_IsHarmless()
    {
        Create().StopPad(42);
    }

    [Fact]
    public void StopPad_ThenTriggeredAgain_SoundsNormally()
    {
        AudioPlaybackService service = Create();
        service.TriggerVoice(3, "/a.wav", new PadVoiceOptions());
        service.StopPad(3);

        service.TriggerVoice(3, "/a.wav", new PadVoiceOptions());

        LastPlayer.Received(1).Play();
        LastPlayer.DidNotReceive().Stop();
    }

    [Fact]
    public void StopAllPads_SilencesEveryVoice()
    {
        AudioPlaybackService service = Create();
        service.TriggerVoice(1, "/a.wav", new PadVoiceOptions());
        service.TriggerVoice(2, "/b.wav", new PadVoiceOptions());

        service.StopAllPads();

        PlayerFor("/a.wav").Received(1).Stop();
        PlayerFor("/b.wav").Received(1).Stop();
    }

    [Fact]
    public void AVoiceThatFinishesOnItsOwn_IsReleased_AndNotStoppedAgainLater()
    {
        AudioPlaybackService service = Create();
        service.TriggerVoice(3, "/a.wav", new PadVoiceOptions());
        IAudioPlayer voice = LastPlayer;

        voice.PlaybackEnded += Raise.Event();
        service.StopPad(3);

        voice.Received(1).Dispose();
        voice.DidNotReceive().Stop();
    }

    [Fact]
    public async Task VoiceWithAMaximumLength_IsCutOffOnceThatTimeIsUp()
    {
        AudioPlaybackService service = Create();

        service.TriggerVoice(3, "/long.wav", new PadVoiceOptions(MaxLength: TimeSpan.FromMilliseconds(60)));
        IAudioPlayer voice = LastPlayer;
        voice.DidNotReceive().Stop();

        await WaitUntilAsync(() => voice.ReceivedCalls().Any(call => call.GetMethodInfo().Name == nameof(IAudioPlayer.Stop)));
        voice.Received(1).Stop();
        voice.Received(1).Dispose();
    }

    [Fact]
    public async Task VoiceWithAMaximumLength_AlreadyStopped_IsNotStoppedTwice()
    {
        AudioPlaybackService service = Create();
        service.TriggerVoice(3, "/long.wav", new PadVoiceOptions(MaxLength: TimeSpan.FromMilliseconds(60)));
        IAudioPlayer voice = LastPlayer;

        service.StopPad(3);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        voice.Received(1).Stop();
    }

    [Fact]
    public void TriggerPad_PadWithASample_SoundsItAtThePadsVolumeAndLooping()
    {
        AudioPlaybackService service = Create();
        LaunchpadPad pad = new() { Bank = 1, Index = 2, ClipPath = "/pad.wav", Volume = 0.4, IsLooping = true };

        service.TriggerPad(pad);

        IAudioPlayer voice = PlayerFor("/pad.wav");
        Assert.Equal(0.4, voice.Volume);
        Assert.True(voice.Loop);
        service.StopPad(pad.VoiceKey);
        voice.Received(1).Stop();
    }

    [Fact]
    public void TriggerPad_EmptyPad_SoundsNothing()
    {
        AudioPlaybackService service = Create();

        service.TriggerPad(new LaunchpadPad { Index = 1 });

        Assert.Empty(_players);
    }
    #endregion

    #region Everything at once
    [Fact]
    public async Task StopEverything_SilencesTheTransportEveryPadAndEveryChannelAndReleasesAudioFocus()
    {
        AudioPlaybackService service = Create();
        await service.LoadAsync(TestData.Clip("Song", path: "/song.wav"));
        service.TriggerVoice(1, "/pad.wav", new PadVoiceOptions());
        service.PlayChannel(Strip("/channel.wav"));

        service.StopEverything();

        PlayerFor("/song.wav").Received(1).Stop();
        PlayerFor("/pad.wav").Received(1).Stop();
        PlayerFor("/channel.wav").Received(1).Stop();
        _focus.Received(1).AbandonFocus();
    }
    #endregion

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime giveUp = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < giveUp)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}
