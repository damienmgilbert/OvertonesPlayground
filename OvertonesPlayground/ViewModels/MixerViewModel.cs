using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

/// <summary>
/// View model for the mixer page. Manages a collection of mixer channel view models
/// and provides commands to load samples and control playback per channel.
/// </summary>
public partial class MixerViewModel : BaseViewModel
{
    private readonly IAudioPlaybackService _playbackService;
    private readonly IAudioLibraryService _libraryService;

    public MixerViewModel(IAudioPlaybackService playbackService, IAudioLibraryService libraryService)
    {
        _playbackService = playbackService;
        _libraryService = libraryService;
        Title = "Mixer";

        for (var i = 1; i <= 4; i++)
        {
            var strip = new MixerChannelStrip { Name = $"Track {i}" };
            Channels.Add(new MixerChannelViewModel(strip, _playbackService));
        }
    }

    /// <summary>Collection of mixer channel view models shown in the UI.</summary>
    public ObservableCollection<MixerChannelViewModel> Channels { get; } = [];

    [RelayCommand]
    private async Task LoadSampleAsync(MixerChannelViewModel? channel)
    {
        if (channel is null)
        {
            return;
        }

        var clip = await _libraryService.ImportFromPickerAsync();
        if (clip is not null)
        {
            channel.AssignSource(clip.FilePath, clip.Name);
        }
    }

    [RelayCommand]
    private void StopChannel(MixerChannelViewModel? channel) => channel?.Stop();

    [RelayCommand]
    private void StopAll() => _playbackService.StopAllChannels();
}
