using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

public partial class LaunchpadViewModel : BaseViewModel
{
    public const int Rows = 8;
    public const int Columns = 8;

    private static readonly string[] PadPalette =
    [
        "#512BD4", "#D600AA", "#2B9348", "#F77F00",
        "#0077B6", "#9D4EDD", "#E5383B", "#FFB703",
    ];

    private readonly IAudioPlaybackService _playbackService;
    private readonly IAudioLibraryService _libraryService;

    public LaunchpadViewModel(IAudioPlaybackService playbackService, IAudioLibraryService libraryService)
    {
        _playbackService = playbackService;
        _libraryService = libraryService;
        Title = "Launchpad";

        for (var i = 0; i < Rows * Columns; i++)
        {
            Pads.Add(new LaunchpadPadViewModel(new LaunchpadPad { Index = i }));
        }
    }

    public ObservableCollection<LaunchpadPadViewModel> Pads { get; } = [];

    [RelayCommand]
    private void Trigger(LaunchpadPadViewModel? pad)
    {
        if (pad is null || !pad.HasClip)
        {
            return;
        }

        _playbackService.TriggerPad(pad.Pad);
    }

    [RelayCommand]
    private void StopPad(LaunchpadPadViewModel? pad)
    {
        if (pad is null)
        {
            return;
        }

        _playbackService.StopPad(pad.Index);
    }

    [RelayCommand]
    private void ToggleLoop(LaunchpadPadViewModel? pad) => pad?.ToggleLoop();

    [RelayCommand]
    private async Task AssignAsync(LaunchpadPadViewModel? pad)
    {
        if (pad is null)
        {
            return;
        }

        var clip = await _libraryService.ImportFromPickerAsync();
        if (clip is null)
        {
            return;
        }

        var color = PadPalette[pad.Index % PadPalette.Length];
        pad.Assign(clip.FilePath, clip.Name, color);
    }

    [RelayCommand]
    private void StopAll() => _playbackService.StopAllPads();
}
