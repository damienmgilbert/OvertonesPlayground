using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
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

    private const string LayoutFileName = "launchpad.json";
    private const string EditHint = "Edit mode: tap a pad to assign a sample, turn looping on or off, stop it, or clear it";
    private const string PlayHint = "Tap a pad to trigger it \u2022 Double-tap to assign a sample \u2022 Triple-tap to toggle looping \u2022 Long-press for more";
    #endregion

    #region Fields
    private static string LayoutFilePath => Path.Combine(FileSystem.AppDataDirectory, LayoutFileName);

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

        RestoreLayout();
    }
    #endregion

    #region Private methods
    partial void OnIsEditModeChanged(bool value)
    {
        Log_EditModeChanged(value);
        OnPropertyChanged(nameof(EditModeText));
        OnPropertyChanged(nameof(EditModeGlyph));
        OnPropertyChanged(nameof(HintText));
    }

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
            SaveLayout();
        }
        catch (Exception ex)
        {
            Log_AssignSampleFailed(ex, pad.Index);
            StatusMessage = "Couldn't import that sample.";
        }
    }

    ///<summary>
    ///Stops a pad and unassigns its sample.
    ///</summary>
    [RelayCommand]
    private void ClearPad(LaunchpadPadViewModel? pad)
    {
        if (pad is null)
        {
            return;
        }

        StopPad(pad);
        Log_PadCleared(pad.Index);
        pad.Clear();
        SaveLayout();
    }

    ///<summary>
    ///Puts back the pads that had a sample when the app last ran. A pad whose sample file has since been deleted stays empty.
    ///The layout is saved after every change, so it is already on disk whenever Android stops or reclaims the app.
    ///</summary>
    private void RestoreLayout()
    {
        try
        {
            if (!File.Exists(LayoutFilePath))
            {
                return;
            }

            List<LaunchpadPad> saved = JsonSerializer.Deserialize<List<LaunchpadPad>>(File.ReadAllText(LayoutFilePath)) ?? [];
            int restored = 0;
            foreach (LaunchpadPad pad in saved)
            {
                if (pad.Index < 0 || pad.Index >= Pads.Count || !pad.HasClip || !File.Exists(pad.ClipPath))
                {
                    continue;
                }

                LaunchpadPadViewModel target = Pads[pad.Index];
                target.Assign(pad.ClipPath, pad.Label, pad.ColorHex);
                target.Pad.Volume = pad.Volume;
                if (pad.IsLooping)
                {
                    target.ToggleLoop();
                }

                restored++;
            }

            Log_LayoutRestored(restored);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Log_LayoutRestoreFailed(ex);
        }
    }

    ///<summary>
    ///Writes the assigned pads to app storage.
    ///</summary>
    private void SaveLayout()
    {
        try
        {
            File.WriteAllText(LayoutFilePath, JsonSerializer.Serialize(Pads.Where(p => p.HasClip).Select(p => p.Pad).ToList()));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log_LayoutSaveFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Restored {PadCount} launchpad pads.")]
    private partial void Log_LayoutRestored(int padCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Couldn't restore the launchpad layout.")]
    private partial void Log_LayoutRestoreFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Couldn't save the launchpad layout.")]
    private partial void Log_LayoutSaveFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating LaunchpadViewModel with {Rows} rows and {Columns} columns.")]
    partial void ConstructorLog(int Rows, int Columns);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Assigned clip '{ClipName}' to pad {PadIndex}.")]
    private partial void Log_AssignedClip(string clipName, int padIndex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to assign a sample to pad {PadIndex}.")]
    private partial void Log_AssignSampleFailed(Exception exception, int padIndex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Edit mode set to {IsEditMode}.")]
    private partial void Log_EditModeChanged(bool isEditMode);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Pad {PadIndex} cleared.")]
    private partial void Log_PadCleared(int padIndex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Pad {PadIndex} loop toggled.")]
    private partial void Log_PadLoopToggled(int padIndex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Opening the menu for pad {PadIndex}.")]
    private partial void Log_PadMenuOpened(int padIndex);

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
    ///Asks the page to show the pad's action menu (assign, loop, stop, clear). Reached by long-pressing a pad, or by
    ///tapping it in edit mode.
    ///</summary>
    [RelayCommand]
    private void OpenPadMenu(LaunchpadPadViewModel? pad)
    {
        if (pad is null)
        {
            return;
        }

        Log_PadMenuOpened(pad.Index);
        PadMenuRequested?.Invoke(this, new LaunchpadPadEventArgs(pad));
    }

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
    ///Switches edit mode on or off. In edit mode a plain tap on a pad opens its menu instead of playing it, so every pad
    ///action can be reached without a multi-tap or long-press gesture.
    ///</summary>
    [RelayCommand]
    private void ToggleEditMode() => IsEditMode = !IsEditMode;

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
        SaveLayout();
    }

    ///<summary>
    ///Fires a new voice for the tapped pad, if it has a sample assigned.
    ///</summary>
    [RelayCommand]
    private void Trigger(LaunchpadPadViewModel? pad)
    {
        if (IsEditMode)
        {
            OpenPadMenu(pad);
            return;
        }

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
    ///Icon of the toolbar button that toggles edit mode: a check mark while editing, a pencil otherwise.
    ///</summary>
    public string EditModeGlyph => IsEditMode ? IconFont.Check : IconFont.Edit;

    ///<summary>
    ///Name of the toolbar button that toggles edit mode. The button shows only its icon, so this is what a screen reader
    ///announces.
    ///</summary>
    public string EditModeText => IsEditMode ? "Done" : "Edit";

    ///<summary>
    ///Hint line above the grid, describing the gestures for the current mode.
    ///</summary>
    public string HintText => IsEditMode ? EditHint : PlayHint;

    ///<summary>
    ///Whether tapping a pad opens its menu (true) or plays it (false).
    ///</summary>
    [ObservableProperty]
    public partial bool IsEditMode { get; set; }

    ///<summary>
    ///Collection of pad view models backing the UI grid.
    ///</summary>
    public ObservableCollection<LaunchpadPadViewModel> Pads { get; } = [];
    #endregion

    #region Public events
    ///<summary>
    ///Raised when a pad's action menu should be shown. The page owns the native action sheet.
    ///</summary>
    public event EventHandler<LaunchpadPadEventArgs>? PadMenuRequested;
    #endregion
}
