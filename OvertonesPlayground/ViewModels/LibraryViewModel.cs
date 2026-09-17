using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;
using System.Collections.ObjectModel;

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
    public LibraryViewModel(IAudioLibraryService libraryService, IAudioPlaybackService playbackService, ILogger<LibraryViewModel> logger) : base(logger)
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
        if (clip is null)
        {
            return;
        }

        _logger.LogDebug("Deleting clip '{ClipName}' ({ClipId}).", clip.Name, clip.Id);
        await _libraryService.DeleteClipAsync(clip);
        Clips.Remove(clip);
    }

    [RelayCommand]
    private async Task EditAsync(AudioClip? clip)
    {
        if (clip is null)
        {
            return;
        }

        _logger.LogDebug("Navigating to editor for clip '{ClipName}' ({ClipId}).", clip.Name, clip.Id);
        await Shell.Current.GoToAsync($"editor?clipId={clip.Id}");
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        _logger.LogDebug("Importing a clip from the picker.");
        AudioClip? clip = await _libraryService.ImportFromPickerAsync();
        if (clip is not null)
        {
            _logger.LogDebug("Imported clip '{ClipName}'.", clip.Name);
            Clips.Insert(0, clip);
        }
        else
        {
            _logger.LogDebug("Import canceled.");
        }
    }

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
            IReadOnlyList<AudioClip> clips = await _libraryService.GetClipsAsync();
            _logger.LogDebug("Loaded {ClipCount} clips.", clips.Count);
            Clips.Clear();
            foreach (AudioClip clip in clips)
            {
                Clips.Add(clip);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnViewModeChanged(LibraryViewMode value) { Preferences.Default.Set(ViewModeKey, value.ToString()); }
    [RelayCommand]
    private async Task PlayAsync(AudioClip? clip)
    {
        if (clip is null)
        {
            return;
        }

        _logger.LogDebug("Playing clip '{ClipName}' ({ClipId}).", clip.Name, clip.Id);
        await _playbackService.LoadAsync(clip);
        _playbackService.Play();
        await Shell.Current.GoToAsync("//player");
    }

    [RelayCommand]
    private void SetViewMode(LibraryViewMode mode)
    {
        _logger.LogDebug("View mode changed to {ViewMode}.", mode);
        ViewMode = mode;
    }
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
