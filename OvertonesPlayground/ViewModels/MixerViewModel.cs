using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

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
