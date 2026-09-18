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

        Log_DeletingClip(clip.Name, clip.Id);
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

        Log_NavigatingToEditor(clip.Name, clip.Id);
        await Shell.Current.GoToAsync($"editor?clipId={clip.Id}");
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        Log_ImportingClip();
        try
        {
            AudioClip? clip = await _libraryService.ImportFromPickerAsync();
            if (clip is not null)
            {
                Log_ImportedClip(clip.Name);
                Clips.Insert(0, clip);
            }
            else
            {
                Log_ImportCanceled();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            Log_ImportFailed(ex);
            StatusMessage = "Couldn't import that file.";
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
            Log_LoadedClips(clips.Count);
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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleting clip '{ClipName}' ({ClipId}).")]
    private partial void Log_DeletingClip(string clipName, string clipId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Import canceled.")]
    private partial void Log_ImportCanceled();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Imported clip '{ClipName}'.")]
    private partial void Log_ImportedClip(string clipName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to import a clip from the picker.")]
    private partial void Log_ImportFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Importing a clip from the picker.")]
    private partial void Log_ImportingClip();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded {ClipCount} clips.")]
    private partial void Log_LoadedClips(int clipCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Navigating to editor for clip '{ClipName}' ({ClipId}).")]
    private partial void Log_NavigatingToEditor(string clipName, string clipId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Playing clip '{ClipName}' ({ClipId}).")]
    private partial void Log_PlayingClip(string clipName, string clipId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "View mode changed to {ViewMode}.")]
    private partial void Log_ViewModeChanged(LibraryViewMode viewMode);

    partial void OnViewModeChanged(LibraryViewMode value) => Preferences.Default.Set(ViewModeKey, value.ToString());

    [RelayCommand]
    private async Task PlayAsync(AudioClip? clip)
    {
        if (clip is null)
        {
            return;
        }

        Log_PlayingClip(clip.Name, clip.Id);
        await _playbackService.LoadAsync(clip);
        _playbackService.Play();
        await Shell.Current.GoToAsync("//player");
    }

    [RelayCommand]
    private void SetViewMode(LibraryViewMode mode)
    {
        Log_ViewModeChanged(mode);
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
