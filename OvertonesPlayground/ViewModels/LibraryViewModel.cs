using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
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
    private const string ViewModeKey = "library_view_mode";

    private readonly IAudioLibraryService _libraryService;
    private readonly IAudioPlaybackService _playbackService;

    public LibraryViewModel(IAudioLibraryService libraryService, IAudioPlaybackService playbackService)
    {
        _libraryService = libraryService;
        _playbackService = playbackService;
        Title = "Library";

        var savedMode = Preferences.Default.Get(ViewModeKey, nameof(LibraryViewMode.Detail));
        ViewMode = Enum.TryParse<LibraryViewMode>(savedMode, out var mode) ? mode : LibraryViewMode.Detail;
    }

    /// <summary>Observable collection of audio clips shown in the library UI.</summary>
    public ObservableCollection<AudioClip> Clips { get; } = [];

    /// <summary>How the library is currently laid out: list, detail cards, or tiles.</summary>
    [ObservableProperty]
    public partial LibraryViewMode ViewMode { get; set; }

    partial void OnViewModeChanged(LibraryViewMode value) =>
        Preferences.Default.Set(ViewModeKey, value.ToString());

    [RelayCommand]
    private void SetViewMode(LibraryViewMode mode) => ViewMode = mode;

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
