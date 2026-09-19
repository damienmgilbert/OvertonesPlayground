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
    private readonly INavigationService _navigation;
    private readonly IAudioPlaybackService _playbackService;
    private readonly IPreferences _preferences;
    #endregion

    #region Constructors
    public LibraryViewModel(IAudioLibraryService libraryService, IAudioPlaybackService playbackService, INavigationService navigation, IPreferences preferences, ILogger<LibraryViewModel> logger) : base(logger)
    {
        _libraryService = libraryService;
        _playbackService = playbackService;
        _navigation = navigation;
        _preferences = preferences;
        Title = "Library";

        string savedMode = _preferences.Get(ViewModeKey, nameof(LibraryViewMode.Detail));
        // TryParse also accepts any number, so a stored "7" has to be rejected explicitly or it would become a layout that doesn't exist.
        ViewMode = Enum.TryParse<LibraryViewMode>(savedMode, out LibraryViewMode mode) && Enum.IsDefined(mode) ? mode : LibraryViewMode.Detail;
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
        await _navigation.GoToAsync($"editor?clipId={clip.Id}");
    }

    ///<summary>
    ///Picks an audio file and imports it, showing progress while it does. The command can't be started again while it is
    ///running.
    ///</summary>
    [RelayCommand]
    private async Task ImportAsync()
    {
        Log_ImportingClip();
        StatusMessage = null;
        ImportFraction = 0;
        ImportStatus = "Preparing the file...";
        IsImporting = true;
        try
        {
            Progress<ImportProgress> progress = new(OnImportProgress);
            AudioClip? clip = await _libraryService.ImportFromPickerAsync(progress);
            if (clip is not null)
            {
                Log_ImportedClip(clip.Name);

                // Rebuild the list the same way the page does when it appears, rather than inserting the one clip.
                await RefreshClipsAsync();
            }
            else
            {
                Log_ImportCanceled();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or OutOfMemoryException)
        {
            Log_ImportFailed(ex);
            StatusMessage = "Couldn't import that file.";
        }
        finally
        {
            IsImporting = false;
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
            await RefreshClipsAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    ///<summary>
    ///Shows an import's progress. Reports arrive on the UI thread but slightly after they are made, so one can land once the
    ///import is over; that one is ignored.
    ///</summary>
    private void OnImportProgress(ImportProgress progress)
    {
        if (!IsImporting)
        {
            return;
        }

        string stage = progress.Stage switch
        {
            ImportStage.Copying => "Copying the file...",
            ImportStage.Decoding => "Converting to WAV...",
            ImportStage.Finishing => "Adding to your library...",
            _ => string.Empty,
        };

        ImportFraction = progress.Fraction;
        ImportStatus = $"{stage} {progress.Fraction:P0}";
    }

    ///<summary>
    ///Replaces the contents of <see cref="Clips"/> with the library's clips, newest first.
    ///</summary>
    private async Task RefreshClipsAsync()
    {
        IReadOnlyList<AudioClip> clips = await _libraryService.GetClipsAsync();
        Log_LoadedClips(clips.Count);
        Clips.Clear();
        foreach (AudioClip clip in clips)
        {
            Clips.Add(clip);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleting clip '{ClipName}' ({ClipId}).")]
    private partial void Log_DeletingClip(string clipName, string clipId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to export clip '{ClipName}' as {Format}.")]
    private partial void Log_ExportFailed(Exception exception, string clipName, AudioExportFormat format);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Exporting clip '{ClipName}' as {Format}.")]
    private partial void Log_ExportingClip(string clipName, AudioExportFormat format);

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

    partial void OnViewModeChanged(LibraryViewMode value) => _preferences.Set(ViewModeKey, value.ToString());

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
        await _navigation.GoToAsync("//player");
    }

    [RelayCommand]
    private void SetViewMode(LibraryViewMode mode)
    {
        Log_ViewModeChanged(mode);
        ViewMode = mode;
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Encodes <paramref name="clip"/> to <paramref name="format"/> and exports it to the shared Music folder. Called
    ///directly from the page's code-behind, once it already knows which format the user picked from an action sheet.
    ///</summary>
    public async Task ExportClipAsync(AudioClip clip, AudioExportFormat format)
    {
        ArgumentNullException.ThrowIfNull(clip);

        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            Log_ExportingClip(clip.Name, format);
            string? location = await _libraryService.ExportClipAsync(clip, format);
            StatusMessage = location is not null
                ? $"Exported '{clip.Name}' to {location}."
                : $"Encoded '{clip.Name}', but couldn't save it to shared storage.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            Log_ExportFailed(ex, clip.Name, format);
            StatusMessage = format == AudioExportFormat.Mp3
                ? $"Couldn't export '{clip.Name}' as MP3 - many Android devices don't have an MP3 encoder. Try AAC instead."
                : $"Couldn't export '{clip.Name}'.";
        }
        finally
        {
            IsBusy = false;
        }
    }
    #endregion

    #region Public properties
    ///<summary>
    ///Observable collection of audio clips shown in the library UI.
    ///</summary>
    public ObservableCollection<AudioClip> Clips { get; } = [];

    ///<summary>
    ///How far through the current import it is, from 0 to 1. Meaningful while <see cref="IsImporting"/> is true.
    ///</summary>
    [ObservableProperty]
    public partial double ImportFraction { get; set; }

    ///<summary>
    ///What the current import is doing, with its percentage. Meaningful while <see cref="IsImporting"/> is true.
    ///</summary>
    [ObservableProperty]
    public partial string ImportStatus { get; set; } = string.Empty;

    ///<summary>
    ///True from when the user taps Import until the clip is in the library, the picker is canceled, or the import fails.
    ///</summary>
    [ObservableProperty]
    public partial bool IsImporting { get; set; }

    ///<summary>
    ///How the library is currently laid out: list, detail cards, or tiles.
    ///</summary>
    [ObservableProperty]
    public partial LibraryViewMode ViewMode { get; set; }
    #endregion
}
