using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

///<summary>
///View model for the Launchpad screen. Manages a grid of pads and exposes commands to trigger, assign and control pad
///playback.
///</summary>
public partial class LaunchpadViewModel : BaseViewModel
{
    #region Constants

    ///<summary>
    ///Number of pad columns in the grid.
    ///</summary>
    public const int Columns = 8;
    ///<summary>
    ///Number of pad rows in the grid.
    ///</summary>
    public const int Rows = 8;
    #endregion

    #region Fields
    ///<summary>
    ///Colors assigned round-robin to pads as they're given a sample.
    ///</summary>
    private static readonly string[] PadPalette = ["#512BD4", "#D600AA", "#2B9348", "#F77F00", "#0077B6", "#9D4EDD", "#E5383B", "#FFB703",];
    private readonly IAudioLibraryService _libraryService;
    private readonly IAudioPlaybackService _playbackService;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the view model and fills the grid with <see cref="Rows"/> x <see cref="Columns"/> empty pads.
    ///</summary>
    public LaunchpadViewModel(IAudioPlaybackService playbackService, IAudioLibraryService libraryService, ILogger<LaunchpadViewModel> logger) : base(logger)
    {
        ConstructorLog(Rows, Columns);
        _playbackService = playbackService;
        _libraryService = libraryService;
        Title = "Launchpad";

        for (int i = 0; i < Rows * Columns; i++)
        {
            Pads.Add(new LaunchpadPadViewModel(new LaunchpadPad { Index = i }));
        }
    }
    #endregion

    #region Private methods
    ///<summary>
    ///Opens the file picker and assigns the chosen sample (and a palette color) to a pad.
    ///</summary>
    [RelayCommand]
    private async Task AssignAsync(LaunchpadPadViewModel? pad)
    {
        if (pad is null)
        {
            return;
        }

        try
        {
            AudioClip? clip = await _libraryService.ImportFromPickerAsync();
            if (clip is null)
            {
                return;
            }

            string color = PadPalette[pad.Index % PadPalette.Length];
            Log_AssignedClip(clip.Name, pad.Index);
            pad.Assign(clip.FilePath, clip.Name, color);
        }
        catch (Exception ex)
        {
            Log_AssignSampleFailed(ex, pad.Index);
            StatusMessage = "Couldn't import that sample.";
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating LaunchpadViewModel with {Rows} rows and {Columns} columns.")]
    partial void ConstructorLog(int Rows, int Columns);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Assigned clip '{ClipName}' to pad {PadIndex}.")]
    private partial void Log_AssignedClip(string clipName, int padIndex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to assign a sample to pad {PadIndex}.")]
    private partial void Log_AssignSampleFailed(Exception exception, int padIndex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Pad {PadIndex} loop toggled.")]
    private partial void Log_PadLoopToggled(int padIndex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to stop all pads.")]
    private partial void Log_StopAllPadsFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to stop pad {PadIndex}.")]
    private partial void Log_StopPadFailed(Exception exception, int padIndex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Stopping all pads.")]
    private partial void Log_StoppingAllPads();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Stopping pad {PadIndex}.")]
    private partial void Log_StoppingPad(int padIndex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Triggering pad {PadIndex}.")]
    private partial void Log_TriggeringPad(int padIndex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to trigger pad {PadIndex}.")]
    private partial void Log_TriggerPadFailed(Exception exception, int padIndex);

    ///<summary>
    ///Stops every currently-sounding pad voice.
    ///</summary>
    [RelayCommand]
    private void StopAll()
    {
        try
        {
            Log_StoppingAllPads();
            _playbackService.StopAllPads();
        }
        catch (Exception ex)
        {
            Log_StopAllPadsFailed(ex);
        }
    }

    ///<summary>
    ///Stops every currently-sounding voice for a single pad.
    ///</summary>
    [RelayCommand]
    private void StopPad(LaunchpadPadViewModel? pad)
    {
        if (pad is null)
        {
            return;
        }

        try
        {
            Log_StoppingPad(pad.Index);
            _playbackService.StopPad(pad.Index);
        }
        catch (Exception ex)
        {
            Log_StopPadFailed(ex, pad.Index);
        }
    }

    ///<summary>
    ///Toggles whether a pad loops its sample instead of playing a one-shot.
    ///</summary>
    [RelayCommand]
    private void ToggleLoop(LaunchpadPadViewModel? pad)
    {
        if (pad is null)
        {
            return;
        }

        Log_PadLoopToggled(pad.Index);
        pad.ToggleLoop();
    }

    ///<summary>
    ///Fires a new voice for the tapped pad, if it has a sample assigned.
    ///</summary>
    [RelayCommand]
    private void Trigger(LaunchpadPadViewModel? pad)
    {
        if (pad is null || !pad.HasClip)
        {
            return;
        }

        try
        {
            Log_TriggeringPad(pad.Index);
            _playbackService.TriggerPad(pad.Pad);
        }
        catch (Exception ex)
        {
            Log_TriggerPadFailed(ex, pad.Index);
            StatusMessage = "Couldn't play that pad.";
        }
    }
    #endregion

    #region Public properties
    ///<summary>
    ///Collection of pad view models backing the UI grid.
    ///</summary>
    public ObservableCollection<LaunchpadPadViewModel> Pads { get; } = [];
    #endregion
}
