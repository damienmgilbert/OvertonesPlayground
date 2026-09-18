using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

///<summary>
///View model for the Multi-Track page: a small fixed set of tracks you place library clips onto, then bounce down to
///a single new library clip via <see cref="IMixdownService"/>. "Mix All to Start" and each track's "Merge" are thin
///one-tap helpers over the same mixdown engine, not separate features.
///</summary>
public partial class MultiTrackViewModel : BaseViewModel
{
    #region Constants
    private const int TrackCount = 4;
    #endregion

    #region Fields
    private readonly IAudioLibraryService _libraryService;
    private readonly IMixdownService _mixdownService;
    #endregion

    #region Constructors
    public MultiTrackViewModel(IMixdownService mixdownService, IAudioLibraryService libraryService, ILoggerFactory loggerFactory, ILogger<MultiTrackViewModel> logger) : base(logger)
    {
        _mixdownService = mixdownService;
        _libraryService = libraryService;
        Title = "Multi-Track";

        for (int i = 1; i <= TrackCount; i++)
        {
            Track track = new() { Name = $"Track {i}" };
            TrackViewModel trackViewModel = new(track, loggerFactory.CreateLogger<TrackViewModel>());
            trackViewModel.PropertyChanged += OnTrackPropertyChanged;
            Tracks.Add(trackViewModel);
        }
    }
    #endregion

    #region Private methods
    [RelayCommand(CanExecute = nameof(CanBounce))]
    private async Task BounceAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            MixProject project = new() { Name = ProjectName, Tracks = [.. Tracks.Select(t => t.Track)], };
            string outputPath = await _mixdownService.RenderAsync(project, ProjectName);
            AudioClip saved = await _libraryService.AddClipAsync(outputPath, ProjectName, isUserRecording: true);
            Log_Bounced(saved.Name);
            StatusMessage = $"Saved '{saved.Name}' to your library.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or InvalidOperationException)
        {
            Log_BounceFailed(ex);
            StatusMessage = "Couldn't bounce the project - add at least one clip to an unmuted track.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    ///<summary>
    ///Gates <see cref="BounceCommand"/> so it can't run with a blank project name, which <see cref="IMixdownService.RenderAsync"/>
    ///would otherwise reject with an unhandled <see cref="ArgumentException"/>.
    ///</summary>
    private bool CanBounce() => !string.IsNullOrWhiteSpace(ProjectName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Bounced the multi-track project to '{ClipName}'.")]
    private partial void Log_Bounced(string clipName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to bounce the multi-track project.")]
    private partial void Log_BounceFailed(Exception exception);

    [RelayCommand]
    private void MixAllToStart()
    {
        foreach (TrackViewModel track in Tracks)
        {
            foreach (TrackClipViewModel clip in track.Clips)
            {
                clip.StartOffsetSeconds = 0;
            }
        }
    }

    ///<summary>
    ///Re-evaluates solo dimming whenever any track's solo state changes, matching the Mixer page's behavior.
    ///</summary>
    private void OnTrackPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TrackViewModel.IsSoloed))
        {
            return;
        }

        bool anySoloed = Tracks.Any(t => t.IsSoloed);
        foreach (TrackViewModel track in Tracks)
        {
            track.IsDimmed = anySoloed && !track.IsSoloed;
        }
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Every clip currently in the library, for the "Add Clip" picker.
    ///</summary>
    public Task<IReadOnlyList<AudioClip>> GetLibraryClipsAsync() => _libraryService.GetClipsAsync();
    #endregion

    #region Public properties
    ///<summary>
    ///Name the bounced mixdown is saved to the library under.
    ///</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BounceCommand))]
    public partial string ProjectName { get; set; } = "Mix";

    ///<summary>
    ///The fixed set of tracks shown on the page.
    ///</summary>
    public ObservableCollection<TrackViewModel> Tracks { get; } = [];
    #endregion
}
