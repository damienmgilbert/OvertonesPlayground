using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.ViewModels;

///<summary>
///View model for the Launchpad screen, a copy of the Novation Launchpad Pro MK3: an 8 x 8 grid of pads ringed by buttons. The
///pads are samples, notes, chords or sequencer steps depending on the mode; the ring of buttons around them each does one
///different job (see <see cref="LaunchpadControl"/>). The class is split by concern: this file holds the state and the pad
///and key entry points, and the other files hold the pad drawing, the buttons, the sound, the sequencer and the saved state.
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

    private const string EditHint = "Edit mode: tap a pad to assign a sample, turn looping on or off, stop it, or clear it";
    private const string PlayHint = "Tap a pad to play it • Long-press it, or use the pencil, to assign a sample, loop or clear it";
    #endregion

    #region Fields

    ///<summary>
    ///How long a pad or button flashes to acknowledge a press that has no other visible effect.
    ///</summary>
    private static readonly TimeSpan FlashTime = TimeSpan.FromMilliseconds(140);

    ///<summary>
    ///The colors of the eight pad columns, taken from the hardware; a sample's pad is lit in its column's color.
    ///</summary>
    private static readonly string[] ColumnColors = ["#E9EC9E", "#EE857F", "#82C2EE", "#DC8AEB", "#72E6E6", "#84E68E", "#EADF8E", "#AA90F5",];

    private readonly LaunchpadPad[][] _banks;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly IAudioLibraryService _libraryService;
    private readonly IMixdownService _mixdownService;
    private readonly IAudioPlaybackService _playbackService;
    private readonly Random _random = new();
    private readonly ISoundSynthesisService _synthesisService;
    private LaunchpadPadViewModel? _flashedPad;
    private LaunchpadProject _project = new();
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the view model, fills the grid with <see cref="Rows"/> x <see cref="Columns"/> empty pads, builds the ring of
    ///buttons and restores the layout the app last saved.
    ///</summary>
    public LaunchpadViewModel(IAudioPlaybackService playbackService, IAudioLibraryService libraryService, ISoundSynthesisService synthesisService, IMixdownService mixdownService, ILogger<LaunchpadViewModel> logger) : base(logger)
    {
        ConstructorLog(Rows, Columns);
        _playbackService = playbackService;
        _libraryService = libraryService;
        _synthesisService = synthesisService;
        _mixdownService = mixdownService;
        Title = "Launchpad";

        _banks = [.. Enumerable.Range(0, LaunchpadProject.BankCount).Select(bank => Enumerable.Range(0, LaunchpadProject.PadsPerBank).Select(index => new LaunchpadPad { Bank = bank, Index = index }).ToArray())];
        foreach (LaunchpadPad pad in _banks[0])
        {
            Pads.Add(new LaunchpadPadViewModel(pad));
        }

        BuildKeys();
        RestoreLayout();
        RefreshAll();
    }
    #endregion

    #region Private methods
    partial void OnIsEditModeChanged(bool value)
    {
        Log_EditModeChanged(value);
        foreach (LaunchpadPadViewModel pad in Pads)
        {
            pad.IsEditMode = value;
        }

        OnPropertyChanged(nameof(EditModeText));
        OnPropertyChanged(nameof(EditModeGlyph));
        RefreshTexts();
    }

    partial void OnIsPlayingChanged(bool value) => RefreshAll();

    partial void OnIsShiftLatchedChanged(bool value) => RefreshKeys();

    partial void OnLayerChanged(LaunchpadLayer value) => RefreshAll();

    partial void OnModeChanged(LaunchpadMode value) => RefreshAll();

    partial void OnPlayheadStepChanged(int value) => RefreshPads();

    partial void OnSelectedTrackChanged(int value) => RefreshAll();

    partial void OnToolChanged(LaunchpadTool value) => RefreshAll();

    ///<summary>
    ///Opens the file picker and assigns the chosen sample to a pad, lit in its column's color.
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

            PushUndo();
            Log_AssignedClip(clip.Name, pad.Index);
            pad.Assign(clip.FilePath, clip.Name, ColumnColors[pad.Column]);
            Changed();
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

        PushUndo();
        StopPad(pad);
        Log_PadCleared(pad.Index);
        pad.Clear();
        Changed();
    }

    ///<summary>
    ///Asks the page to show the pad's action menu (assign, loop, stop, clear). Reached by long-pressing a pad (except in Custom
    ///mode, where holding plays it), or by tapping it in edit mode.
    ///</summary>
    [RelayCommand]
    private void OpenPadMenu(LaunchpadPadViewModel? pad)
    {
        // In Custom mode a pad is held to be played, so holding it must not open the menu; edit mode still can.
        if (pad is null || IsGateActive)
        {
            return;
        }

        Log_PadMenuOpened(pad.Index);
        PadMenuRequested?.Invoke(this, new LaunchpadPadEventArgs(pad));
    }

    ///<summary>
    ///Stops every currently-sounding pad voice, and the sequencer.
    ///</summary>
    [RelayCommand]
    private void StopAll()
    {
        try
        {
            Log_StoppingAllPads();
            StopTransport();
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
            _playbackService.StopPad(pad.Pad.VoiceKey);
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

        PushUndo();
        Log_PadLoopToggled(pad.Index);
        pad.ToggleLoop();
        Changed();
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Creating LaunchpadViewModel with {Rows} rows and {Columns} columns.")]
    partial void ConstructorLog(int Rows, int Columns);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Assigned clip '{ClipName}' to pad {PadIndex}.")]
    private partial void Log_AssignedClip(string clipName, int padIndex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to assign a sample to pad {PadIndex}.")]
    private partial void Log_AssignSampleFailed(Exception exception, int padIndex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Edit mode set to {IsEditMode}.")]
    private partial void Log_EditModeChanged(bool isEditMode);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Launchpad key {Control} pressed (shifted: {Shifted}).")]
    private partial void Log_KeyPressed(LaunchpadControl control, bool shifted);

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

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to trigger pad {PadIndex}.")]
    private partial void Log_TriggerPadFailed(Exception exception, int padIndex);
    #endregion

    #region Public methods
    ///<summary>
    ///A pad was pressed down. Only Custom mode acts on this, playing the pad for as long as it is held.
    ///</summary>
    public void PadPressed(LaunchpadPadViewModel? pad)
    {
        if (pad is null || !IsGateActive || !pad.HasClip)
        {
            return;
        }

        PlaySample(pad, live: true);
    }

    ///<summary>
    ///A pad was let go. Ends the sound of a pad held in Custom mode.
    ///</summary>
    public void PadReleased(LaunchpadPadViewModel? pad)
    {
        if (pad is not null && IsGateActive && pad.HasClip)
        {
            StopPad(pad);
        }
    }

    ///<summary>
    ///A pad was tapped. What that does depends on the armed tool, the active layer and the mode.
    ///</summary>
    public void PadTapped(LaunchpadPadViewModel? pad)
    {
        if (pad is null)
        {
            return;
        }

        try
        {
            if (Tool != LaunchpadTool.None)
            {
                ApplyTool(pad);
            }
            else if (TryEditLayer(pad))
            {
                // A fader, tempo or sequencer-settings view took the tap.
            }
            else
            {
                PlayPadByMode(pad);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException)
        {
            Log_TriggerPadFailed(ex, pad.Index);
            StatusMessage = "Couldn't play that pad.";
        }
    }
    #endregion

    #region Public properties
    ///<summary>
    ///Whether pads currently sound only while held: Custom mode with no tool, layer or edit mode in the way.
    ///</summary>
    private bool IsGateActive => Mode == LaunchpadMode.Custom && Tool == LaunchpadTool.None && !IsEditMode && ShowsSamples;

    ///<summary>
    ///Which of the four pad banks (0 to 3) is on screen.
    ///</summary>
    [ObservableProperty]
    public partial int Bank { get; set; }

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
    ///Whether tapping a pad opens its menu (true) or plays it (false).
    ///</summary>
    [ObservableProperty]
    public partial bool IsEditMode { get; set; }

    ///<summary>
    ///Whether the sequencer is running.
    ///</summary>
    [ObservableProperty]
    public partial bool IsPlaying { get; set; }

    ///<summary>
    ///Whether Shift is latched, so the next button press runs its second function.
    ///</summary>
    [ObservableProperty]
    public partial bool IsShiftLatched { get; set; }

    ///<summary>
    ///The button layer on top of the mode: a column function, a shifted function or a sequencer layer.
    ///</summary>
    [ObservableProperty]
    public partial LaunchpadLayer Layer { get; set; }

    ///<summary>
    ///What the pads do.
    ///</summary>
    [ObservableProperty]
    public partial LaunchpadMode Mode { get; set; }

    ///<summary>
    ///Collection of pad view models backing the UI grid.
    ///</summary>
    public ObservableCollection<LaunchpadPadViewModel> Pads { get; } = [];

    ///<summary>
    ///The step the sequencer just played, or -1 when it is stopped.
    ///</summary>
    [ObservableProperty]
    public partial int PlayheadStep { get; set; } = -1;

    ///<summary>
    ///The sequencer track (0 to 3) that steps are edited on.
    ///</summary>
    [ObservableProperty]
    public partial int SelectedTrack { get; set; }

    ///<summary>
    ///The edit tool armed from the left-hand buttons.
    ///</summary>
    [ObservableProperty]
    public partial LaunchpadTool Tool { get; set; }
    #endregion

    #region Public events
    ///<summary>
    ///Raised when a list of choices should be shown (the Setup and Projects menus). The page owns the native action sheet.
    ///</summary>
    public event EventHandler<LaunchpadMenuEventArgs>? MenuRequested;

    ///<summary>
    ///Raised when a pad's action menu should be shown. The page owns the native action sheet.
    ///</summary>
    public event EventHandler<LaunchpadPadEventArgs>? PadMenuRequested;
    #endregion

    #region Public delegates
    ///<summary>
    ///Asks the user for a line of text (title, message, initial value) and returns it, or null if they cancel. Set by the
    ///page, which owns the native prompt.
    ///</summary>
    public Func<string, string, string, Task<string?>>? TextPrompt { get; set; }
    #endregion
}
