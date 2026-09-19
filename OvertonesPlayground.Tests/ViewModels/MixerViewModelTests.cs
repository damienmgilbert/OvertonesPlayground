namespace OvertonesPlayground.Tests.ViewModels;

public sealed class MixerViewModelTests
{
    private readonly IAudioLibraryService _library = Substitute.For<IAudioLibraryService>();
    private readonly IAudioPlaybackService _playback = Substitute.For<IAudioPlaybackService>();

    private MixerViewModel Create() => new(_playback, _library, NullLoggerFactory.Instance, NullLogger<MixerViewModel>.Instance);

    private MixerChannelViewModel CreateChannel(MixerChannelStrip? strip = null)
    {
        MixerChannelViewModel channel = new(strip ?? new MixerChannelStrip { Name = "Drums" }, _playback, NullLogger<MixerChannelViewModel>.Instance);
        _playback.ClearReceivedCalls();
        return channel;
    }

    #region The mixer
    [Fact]
    public void Constructor_BuildsFourNumberedEmptyChannelsInDifferentColors()
    {
        MixerViewModel viewModel = Create();

        Assert.Equal("Mixer", viewModel.Title);
        Assert.Equal(["Track 1", "Track 2", "Track 3", "Track 4"], viewModel.Channels.Select(c => c.Name));
        Assert.Equal([1, 2, 3, 4], viewModel.Channels.Select(c => c.Number));
        Assert.Equal(4, viewModel.Channels.Select(c => c.ColorHex).Distinct().Count());
        Assert.All(viewModel.Channels, channel => Assert.False(channel.HasSource));
        Assert.All(viewModel.Channels, channel => Assert.Equal("No sample loaded", channel.SourceLabel));
    }

    [Fact]
    public void Soloing_OneChannel_DimsTheOthers()
    {
        MixerViewModel viewModel = Create();

        viewModel.Channels[1].ToggleSoloCommand.Execute(null);

        Assert.Equal([true, false, true, true], viewModel.Channels.Select(c => c.IsDimmed));
    }

    [Fact]
    public void Soloing_TwoChannels_DimsOnlyTheRest()
    {
        MixerViewModel viewModel = Create();

        viewModel.Channels[0].ToggleSoloCommand.Execute(null);
        viewModel.Channels[3].ToggleSoloCommand.Execute(null);

        Assert.Equal([false, true, true, false], viewModel.Channels.Select(c => c.IsDimmed));
    }

    [Fact]
    public void Soloing_ThenUnsoloingTheLastOne_UndimsEverything()
    {
        MixerViewModel viewModel = Create();
        viewModel.Channels[2].ToggleSoloCommand.Execute(null);

        viewModel.Channels[2].ToggleSoloCommand.Execute(null);

        Assert.All(viewModel.Channels, channel => Assert.False(channel.IsDimmed));
    }

    [Fact]
    public void ChangingSomethingOtherThanSolo_DoesNotTouchDimming()
    {
        MixerViewModel viewModel = Create();
        viewModel.Channels[0].ToggleSoloCommand.Execute(null);

        viewModel.Channels[1].ToggleMuteCommand.Execute(null);

        Assert.True(viewModel.Channels[1].IsDimmed);
        Assert.False(viewModel.Channels[0].IsDimmed);
    }

    [Fact]
    public void StopAll_StopsEveryChannelsVoice()
    {
        MixerViewModel viewModel = Create();
        _playback.ClearReceivedCalls();

        viewModel.StopAllCommand.Execute(null);

        foreach (MixerChannelViewModel channel in viewModel.Channels)
        {
            _playback.Received(1).StopChannel(channel.Channel.Id);
        }
    }

    [Fact]
    public void StopChannel_StopsJustThatChannel()
    {
        MixerViewModel viewModel = Create();
        viewModel.Channels[1].AssignSource("/a.wav", "A");
        viewModel.Channels[1].TogglePlaybackCommand.Execute(null);
        _playback.ClearReceivedCalls();

        viewModel.StopChannelCommand.Execute(viewModel.Channels[1]);

        _playback.Received(1).StopChannel(viewModel.Channels[1].Channel.Id);
        Assert.False(viewModel.Channels[1].IsPlaying);
    }

    [Fact]
    public void StopChannel_NoChannel_DoesNothing()
    {
        Create().StopChannelCommand.Execute(null);
    }

    [Fact]
    public async Task LoadSample_PickedAClip_AssignsItToTheChannelWithoutPlayingIt()
    {
        MixerViewModel viewModel = Create();
        _library.ImportFromPickerAsync().Returns(TestData.Clip("Kick", path: "/clips/kick.wav"));

        await viewModel.LoadSampleCommand.ExecuteAsync(viewModel.Channels[2]);

        Assert.True(viewModel.Channels[2].HasSource);
        Assert.Equal("Kick", viewModel.Channels[2].SourceLabel);
        Assert.Equal("/clips/kick.wav", viewModel.Channels[2].Channel.SourceClipPath);
        Assert.False(viewModel.Channels[2].IsPlaying);
        _playback.DidNotReceiveWithAnyArgs().PlayChannel(default!);
    }

    [Fact]
    public async Task LoadSample_PickerCancelled_LeavesTheChannelAlone()
    {
        MixerViewModel viewModel = Create();
        _library.ImportFromPickerAsync().Returns((AudioClip?)null);

        await viewModel.LoadSampleCommand.ExecuteAsync(viewModel.Channels[0]);

        Assert.False(viewModel.Channels[0].HasSource);
    }

    [Fact]
    public async Task LoadSample_NoChannel_DoesNotOpenThePicker()
    {
        await Create().LoadSampleCommand.ExecuteAsync(null);

        await _library.DidNotReceiveWithAnyArgs().ImportFromPickerAsync();
    }
    #endregion

    #region A channel
    [Fact]
    public void Channel_MirrorsItsStrip()
    {
        MixerChannelStrip strip = new() { Name = "Bass", Volume = 0.6, Pan = -0.4, IsMuted = true, IsSoloed = true, ColorHex = "#123456" };

        MixerChannelViewModel channel = CreateChannel(strip);

        Assert.Equal("Bass", channel.Name);
        Assert.Equal(0.6, channel.Volume);
        Assert.Equal(-0.4, channel.Pan);
        Assert.True(channel.IsMuted);
        Assert.True(channel.IsSoloed);
        Assert.Equal("#123456", channel.ColorHex);
        Assert.Same(strip, channel.Channel);
    }

    [Fact]
    public void Channel_ToggleMute_FlipsItUpdatesTheStripAndTheLiveVoice()
    {
        MixerChannelViewModel channel = CreateChannel();

        channel.ToggleMuteCommand.Execute(null);

        Assert.True(channel.IsMuted);
        Assert.True(channel.Channel.IsMuted);
        _playback.Received(1).UpdateChannel(channel.Channel);

        channel.ToggleMuteCommand.Execute(null);

        Assert.False(channel.Channel.IsMuted);
    }

    [Fact]
    public void Channel_VolumeAndPan_UpdateTheStripAndTheLiveVoice()
    {
        MixerChannelViewModel channel = CreateChannel();

        channel.Volume = 0.25;
        channel.Pan = 0.75;

        Assert.Equal(0.25, channel.Channel.Volume);
        Assert.Equal(0.75, channel.Channel.Pan);
        _playback.Received(2).UpdateChannel(channel.Channel);
    }

    [Fact]
    public void Channel_ToggleSolo_UpdatesTheStrip()
    {
        MixerChannelViewModel channel = CreateChannel();

        channel.ToggleSoloCommand.Execute(null);

        Assert.True(channel.Channel.IsSoloed);
    }

    [Fact]
    public void Channel_Rename_UpdatesTheStrip()
    {
        MixerChannelViewModel channel = CreateChannel();

        channel.Name = "Lead";

        Assert.Equal("Lead", channel.Channel.Name);
    }

    [Fact]
    public void Channel_TogglePlayback_WithoutASample_DoesNothing()
    {
        MixerChannelViewModel channel = CreateChannel();

        channel.TogglePlaybackCommand.Execute(null);

        Assert.False(channel.IsPlaying);
        _playback.DidNotReceiveWithAnyArgs().PlayChannel(default!);
    }

    [Fact]
    public void Channel_TogglePlayback_StartsThenStopsTheVoice()
    {
        MixerChannelViewModel channel = CreateChannel();
        channel.AssignSource("/a.wav", "A");

        channel.TogglePlaybackCommand.Execute(null);
        Assert.True(channel.IsPlaying);
        _playback.Received(1).PlayChannel(channel.Channel);

        channel.TogglePlaybackCommand.Execute(null);
        Assert.False(channel.IsPlaying);
        _playback.Received(1).StopChannel(channel.Channel.Id);
    }

    [Fact]
    public void Channel_AssignSource_WhilePlaying_StopsTheOldSampleFirst()
    {
        MixerChannelViewModel channel = CreateChannel();
        channel.AssignSource("/a.wav", "A");
        channel.TogglePlaybackCommand.Execute(null);

        channel.AssignSource("/b.wav", "B");

        Assert.False(channel.IsPlaying);
        _playback.Received(1).StopChannel(channel.Channel.Id);
        Assert.Equal("B", channel.SourceLabel);
        Assert.Equal("/b.wav", channel.Channel.SourceClipPath);
    }

    [Fact]
    public void Channel_AssignSource_RaisesHasSource()
    {
        MixerChannelViewModel channel = CreateChannel();
        List<string?> raised = [];
        channel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        channel.AssignSource("/a.wav", "A");

        Assert.Contains(nameof(MixerChannelViewModel.HasSource), raised);
        Assert.True(channel.HasSource);
    }
    #endregion
}
