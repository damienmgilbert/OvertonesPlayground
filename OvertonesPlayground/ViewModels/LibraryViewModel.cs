using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

///<summary>
///View model for the library page. Exposes the list of available audio clips and commands to load, import, play, edit
///and delete clips.
///</summary>
public partial class LibraryViewModel : BaseViewModel
{
    #region Constants
    private const string ViewModeKey = "library_view_mode";
    #endregion

    #region Fields
    private readonly IAudioLibraryService _libraryService;
    private readonly IAudioPlaybackService _playbackService;
    #endregion

    #region Constructors
    public LibraryViewModel(IAudioLibraryService libraryService, IAudioPlaybackService playbackService)
    {
        _libraryService = libraryService;
        _playbackService = playbackService;
        Title = "Library";

        string savedMode = Preferences.Default.Get(ViewModeKey, nameof(LibraryViewMode.Detail));
        ViewMode = Enum.TryParse<LibraryViewMode>(savedMode, out LibraryViewMode mode) ? mode : LibraryViewMode.Detail;
    }
    #endregion

    #region Private methods
    [RelayCommand]
    private async Task DeleteAsync(AudioClip? clip)
    {
        if(clip is null)
        {
            return;
        }

        await _libraryService.DeleteClipAsync(clip);
        Clips.Remove(clip);
    }

    [RelayCommand]
    private async Task EditAsync(AudioClip? clip)
    {
        if(clip is null)
        {
            return;
        }

        await Shell.Current.GoToAsync($"editor?clipId={clip.Id}");
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        AudioClip? clip = await _libraryService.ImportFromPickerAsync();
        if(clip is not null)
        {
            Clips.Insert(0, clip);
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if(IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            IReadOnlyList<AudioClip> clips = await _libraryService.GetClipsAsync();
            Clips.Clear();
            foreach(AudioClip clip in clips)
            {
                Clips.Add(clip);
            }
        } finally
        {
            IsBusy = false;
        }
    }

    partial void OnViewModeChanged(LibraryViewMode value) { Preferences.Default.Set(ViewModeKey, value.ToString()); }
    [RelayCommand]
    private async Task PlayAsync(AudioClip? clip)
    {
        if(clip is null)
        {
            return;
        }

        await _playbackService.LoadAsync(clip);
        _playbackService.Play();
        await Shell.Current.GoToAsync("//player");
    }

    [RelayCommand]
    private void SetViewMode(LibraryViewMode mode) { ViewMode = mode; }
    #endregion

    #region Public properties
    ///<summary>
    ///Observable collection of audio clips shown in the library UI.
    ///</summary>
    public ObservableCollection<AudioClip> Clips { get; } = [];

    ///<summary>
    ///How the library is currently laid out: list, detail cards, or tiles.
    ///</summary>
    [ObservableProperty]
    public partial LibraryViewMode ViewMode { get; set; }
    #endregion
}
