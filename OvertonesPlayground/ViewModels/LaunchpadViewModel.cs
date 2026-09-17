using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
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
    private static readonly string[] PadPalette = [ "#512BD4", "#D600AA", "#2B9348", "#F77F00", "#0077B6", "#9D4EDD", "#E5383B", "#FFB703", ];
    private readonly IAudioLibraryService _libraryService;
    private readonly IAudioPlaybackService _playbackService;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the view model and fills the grid with <see cref="Rows"/> x <see cref="Columns"/> empty pads.
    ///</summary>
    public LaunchpadViewModel(IAudioPlaybackService playbackService, IAudioLibraryService libraryService, ILogger<LaunchpadViewModel> logger) : base(logger)
    {
        _playbackService = playbackService;
        _libraryService = libraryService;
        Title = "Launchpad";

        for(int i = 0; i < Rows * Columns; i++)
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
        if(pad is null)
        {
            return;
        }

        try
        {
            AudioClip? clip = await _libraryService.ImportFromPickerAsync();
            if(clip is null)
            {
                return;
            }

            string color = PadPalette[pad.Index % PadPalette.Length];
            Logger.LogDebug("Assigned clip '{ClipName}' to pad {PadIndex}.", clip.Name, pad.Index);
            pad.Assign(clip.FilePath, clip.Name, color);
        } catch(Exception ex)
        {
            Logger.LogError(ex, "Failed to assign a sample to pad {PadIndex}.", pad.Index);
            StatusMessage = "Couldn't import that sample.";
        }
    }

    ///<summary>
    ///Stops every currently-sounding pad voice.
    ///</summary>
    [RelayCommand]
    private void StopAll()
    {
        try
        {
            Logger.LogDebug("Stopping all pads.");
            _playbackService.StopAllPads();
        } catch(Exception ex)
        {
            Logger.LogError(ex, "Failed to stop all pads.");
        }
    }
    ///<summary>
    ///Stops every currently-sounding voice for a single pad.
    ///</summary>
    [RelayCommand]
    private void StopPad(LaunchpadPadViewModel? pad)
    {
        if(pad is null)
        {
            return;
        }

        try
        {
            Logger.LogDebug("Stopping pad {PadIndex}.", pad.Index);
            _playbackService.StopPad(pad.Index);
        } catch(Exception ex)
        {
            Logger.LogError(ex, "Failed to stop pad {PadIndex}.", pad.Index);
        }
    }

    ///<summary>
    ///Toggles whether a pad loops its sample instead of playing a one-shot.
    ///</summary>
    [RelayCommand]
    private void ToggleLoop(LaunchpadPadViewModel? pad)
    {
        if(pad is null)
        {
            return;
        }

        Logger.LogDebug("Pad {PadIndex} loop toggled.", pad.Index);
        pad.ToggleLoop();
    }
    ///<summary>
    ///Fires a new voice for the tapped pad, if it has a sample assigned.
    ///</summary>
    [RelayCommand]
    private void Trigger(LaunchpadPadViewModel? pad)
    {
        if(pad is null || !pad.HasClip)
        {
            return;
        }

        try
        {
            Logger.LogDebug("Triggering pad {PadIndex}.", pad.Index);
            _playbackService.TriggerPad(pad.Pad);
        } catch(Exception ex)
        {
            Logger.LogError(ex, "Failed to trigger pad {PadIndex}.", pad.Index);
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
