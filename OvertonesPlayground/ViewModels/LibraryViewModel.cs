using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

/// <summary>
/// View model for the library page. Exposes the list of available audio clips and
/// commands to load, import, play, edit and delete clips.
/// </summary>
public partial class LibraryViewModel : BaseViewModel
{
    private readonly IAudioLibraryService _libraryService;
    private readonly IAudioPlaybackService _playbackService;

    public LibraryViewModel(IAudioLibraryService libraryService, IAudioPlaybackService playbackService)
    {
        _libraryService = libraryService;
        _playbackService = playbackService;
        Title = "Library";
    }

    /// <summary>Observable collection of audio clips shown in the library UI.</summary>
    public ObservableCollection<AudioClip> Clips { get; } = [];

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var clips = await _libraryService.GetClipsAsync();
            Clips.Clear();
            foreach (var clip in clips)
            {
                Clips.Add(clip);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        var clip = await _libraryService.ImportFromPickerAsync();
        if (clip is not null)
        {
            Clips.Insert(0, clip);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(AudioClip? clip)
    {
        if (clip is null)
        {
            return;
        }

        await _libraryService.DeleteClipAsync(clip);
        Clips.Remove(clip);
    }

    [RelayCommand]
    private async Task PlayAsync(AudioClip? clip)
    {
        if (clip is null)
        {
            return;
        }

        await _playbackService.LoadAsync(clip);
        _playbackService.Play();
        await Shell.Current.GoToAsync("//player");
    }

    [RelayCommand]
    private async Task EditAsync(AudioClip? clip)
    {
        if (clip is null)
        {
            return;
        }

        await Shell.Current.GoToAsync($"editor?clipId={clip.Id}");
    }
}
