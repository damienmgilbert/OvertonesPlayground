using System.Text.Json;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.ViewModels;

///<summary>
///The guided tutorials. A tutorial borrows the pads: it loads a lesson setup built from the Sound Bank, shows one tip at a time,
///and on "Show me" carries out the step on the real Launchpad (through <see cref="ILaunchpadTutorialHost"/>, which does what a
///finger would). The user's own layout is set aside when the tutorial starts, is never saved over while it runs, and is put back
///when it ends or the user leaves.
///</summary>
public partial class LaunchpadViewModel : ILaunchpadTutorialHost
{
    #region Constants
    ///<summary>
    ///How long to leave a pressed button to be seen before the next action.
    ///</summary>
    private const int PressPauseMilliseconds = 200;

    ///<summary>
    ///How long Shift is left lit before the button it applies to is pressed.
    ///</summary>
    private const int ShiftPauseMilliseconds = 60;

    private const string ActionShowMe = "Show me";
    private const string ActionNext = "Next";
    private const string ActionFinish = "Finish";
    private const string CloseExit = "Exit";
    #endregion

    #region Fields
    private TutorialBackup? _backup;
    private int _tutorialGeneration;
    private bool _tutorialIsBusy;
    private bool _tutorialShowsResult;
    private int _tutorialStep;
    private LaunchpadTutorial? _tutorial;
    private CancellationTokenSource? _tutorialCts;

    ///<summary>
    ///What the user had on the Launchpad before a tutorial borrowed it.
    ///</summary>
    private sealed record TutorialBackup(string ProjectJson, List<string> Undo, LaunchpadMode Mode, int SelectedTrack, bool IsQuantiseOn, bool IsRecordQuantiseOn, bool IsClickOn, int FixedIndex);
    #endregion

    #region Private methods
    [LoggerMessage(Level = LogLevel.Warning, Message = "Couldn't load the tutorial setup {LessonId}.")]
    private partial void Log_LessonFailed(string lessonId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "A step of the tutorial {TutorialId} failed.")]
    private partial void Log_TutorialStepFailed(string tutorialId, Exception exception);

    ///<summary>
    ///Waits for <paramref name="milliseconds"/>, until the tutorial is left.
    ///</summary>
    private Task PauseAsync(int milliseconds) =>
        milliseconds <= 0 ? Task.CompletedTask : Delay(TimeSpan.FromMilliseconds(milliseconds), _tutorialCts?.Token ?? CancellationToken.None);

    private LaunchpadPadViewModel PadAt(int pad) => pad is >= 0 and < LaunchpadTutorialFocus.PadCount
        ? Pads[pad]
        : throw new ArgumentOutOfRangeException(nameof(pad), pad, "A pad is numbered 0 to 63.");

    ///<summary>
    ///Puts a lesson setup on the pads, from a fresh start: nothing playing, no tool, layer or edit mode, and the settings that
    ///are not part of a project (quantise, fixed length, click) off.
    ///</summary>
    private void ApplyLesson(LaunchpadProject lesson)
    {
        StopAll();
        ClearTool();
        IsShiftLatched = false;
        IsEditMode = false;
        StopClick();
        _isClickOn = false;
        _isQuantiseOn = false;
        _isRecordQuantiseOn = false;
        _fixedIndex = 0;
        _instrument = null;
        _hits.Clear();
        ApplyProject(lesson);
        Mode = LaunchpadMode.Session;
        Layer = LaunchpadLayer.None;
        SelectedTrack = 0;
        Changed();
    }

    ///<summary>
    ///Draws the tip for the current step, and outlines what it points at.
    ///</summary>
    private void ShowPrompt()
    {
        if (_tutorial is not { } tutorial)
        {
            return;
        }

        LaunchpadTutorialStep step = tutorial.Steps[_tutorialStep];
        bool isLast = _tutorialStep == tutorial.Steps.Count - 1;
        bool canShow = !_tutorialShowsResult && step.Perform is not null;
        string action = canShow ? ActionShowMe : isLast ? ActionFinish : ActionNext;
        string message = _tutorialShowsResult ? step.Result ?? step.Message : step.Message;

        ApplySpotlight(step.Focus);
        TutorialPromptChanged?.Invoke(this, new LaunchpadTutorialPrompt($"{step.Title} ({_tutorialStep + 1}/{tutorial.Steps.Count})", message, action, CloseExit, step.Focus));
    }

    ///<summary>
    ///Outlines the buttons and pads a tutorial step names, and nothing else.
    ///</summary>
    private void ApplySpotlight(LaunchpadTutorialFocus? focus)
    {
        HashSet<LaunchpadControl> keys = focus is null ? [] : [.. focus.HighlightedKeys];
        HashSet<int> tracks = focus is null ? [] : [.. focus.HighlightedTracks];
        HashSet<int> pads = focus is null ? [] : [.. focus.HighlightedPads];
        foreach (LaunchpadKeyViewModel key in AllKeys)
        {
            key.IsSpotlit = key.Control == LaunchpadControl.Track ? tracks.Contains(key.Column) : keys.Contains(key.Control);
        }

        foreach (LaunchpadPadViewModel pad in Pads)
        {
            pad.IsSpotlit = pads.Contains(pad.Index);
        }
    }

    ///<summary>
    ///Goes to a step, or ends the tutorial after the last one.
    ///</summary>
    private void GoToStep(int step)
    {
        if (_tutorial is not { } tutorial)
        {
            return;
        }

        if (step >= tutorial.Steps.Count)
        {
            string title = tutorial.Title;
            EndTutorial();
            Say($"Finished '{title}'. Your own pads are back as you left them.");
            return;
        }

        _tutorialStep = step;
        _tutorialShowsResult = false;
        ShowPrompt();
    }

    ///<summary>
    ///Puts the user's own layout back and clears every trace of the tutorial.
    ///</summary>
    private void EndTutorial()
    {
        _tutorialGeneration++;
        _tutorialCts?.Cancel();
        _tutorialCts?.Dispose();
        _tutorialCts = null;
        TutorialBackup? backup = _backup;
        _backup = null;
        _tutorial = null;
        _tutorialStep = 0;
        _tutorialShowsResult = false;
        _tutorialIsBusy = false;
        ApplySpotlight(null);
        TutorialPromptChanged?.Invoke(this, null);
        if (backup is null)
        {
            return;
        }

        StopAll();
        ClearTool();
        IsShiftLatched = false;
        IsEditMode = false;
        StopClick();
        _isClickOn = false;
        _instrument = null;
        _hits.Clear();
        ApplyProject(ParseProject(backup.ProjectJson));
        _undo.Clear();
        _undo.AddRange(backup.Undo);
        Mode = backup.Mode;
        Layer = LaunchpadLayer.None;
        SelectedTrack = backup.SelectedTrack;
        _isQuantiseOn = backup.IsQuantiseOn;
        _isRecordQuantiseOn = backup.IsRecordQuantiseOn;
        _fixedIndex = backup.FixedIndex;
        if (backup.IsClickOn)
        {
            _isClickOn = true;
            _ = StartClickAsync();
        }

        Changed();
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Starts a tutorial: sets the user's pads aside, loads the tutorial's setup and shows its first tip. Does nothing, and leaves the
    ///pads as they were, if the setup can't be built.
    ///</summary>
    public async Task StartTutorialAsync(LaunchpadTutorial tutorial)
    {
        ArgumentNullException.ThrowIfNull(tutorial);
        if (IsTutorialActive)
        {
            ExitTutorial();
        }

        IsBusy = true;
        try
        {
            Say($"Loading '{tutorial.Title}'...");
            LaunchpadProject lesson = await _examples.CreateLessonAsync(tutorial.LessonId);
            StopAll();
            _backup = new TutorialBackup(JsonSerializer.Serialize(BuildProject()), [.. _undo], Mode, SelectedTrack, _isQuantiseOn, _isRecordQuantiseOn, _isClickOn, _fixedIndex);
            _undo.Clear();
            _tutorial = tutorial;
            _tutorialCts = new CancellationTokenSource();
            _tutorialGeneration++;
            ApplyLesson(lesson);
            Say($"'{tutorial.Title}': your own pads will be back when it ends.");
            GoToStep(0);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Log_LessonFailed(tutorial.LessonId, ex);
            if (_tutorial is not null)
            {
                EndTutorial();
            }

            Say($"Couldn't start '{tutorial.Title}'.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    ///<summary>
    ///The tip's main button was pressed: carries out the step ("Show me"), or goes on to the next one.
    ///</summary>
    public async Task ContinueTutorialAsync()
    {
        if (_tutorial is not { } tutorial || _tutorialIsBusy)
        {
            return;
        }

        int generation = _tutorialGeneration;
        LaunchpadTutorialStep step = tutorial.Steps[_tutorialStep];
        _tutorialIsBusy = true;
        try
        {
            if (!_tutorialShowsResult && step.Perform is not null)
            {
                // The tip steps aside while the step plays, so the pads can be seen.
                TutorialPromptChanged?.Invoke(this, null);
                try
                {
                    await step.Perform(this);
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
                {
                    Log_TutorialStepFailed(tutorial.Id, ex);
                    Say("That step couldn't be played; carry on with Next.");
                }

                if (generation != _tutorialGeneration)
                {
                    return;
                }

                if (step.Result is not null)
                {
                    _tutorialShowsResult = true;
                    ShowPrompt();
                    return;
                }
            }

            GoToStep(_tutorialStep + 1);
        }
        catch (OperationCanceledException)
        {
            // The user left the tutorial while a step was playing.
        }
        finally
        {
            if (generation == _tutorialGeneration)
            {
                _tutorialIsBusy = false;
            }
        }
    }

    ///<summary>
    ///Leaves the tutorial, if one is running, and puts the user's pads back.
    ///</summary>
    public void ExitTutorial()
    {
        if (_tutorial is not null)
        {
            EndTutorial();
        }
    }
    #endregion

    #region Public events
    ///<summary>
    ///Raised with the tip to show, or with null when the tip should go away (a step is playing, or the tutorial ended). The page
    ///draws the tip; whether a tutorial is still running is <see cref="IsTutorialActive"/>.
    ///</summary>
    public event EventHandler<LaunchpadTutorialPrompt?>? TutorialPromptChanged;
    #endregion

    #region Public properties
    ///<summary>
    ///Waits for a length of time; the tutorial's pauses go through this, so the tests can skip them.
    ///</summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = Task.Delay;

    ///<summary>
    ///Whether a tutorial is running.
    ///</summary>
    public bool IsTutorialActive => _tutorial is not null;

    ///<summary>
    ///The tutorials, in the order they are taught.
    ///</summary>
    public static IReadOnlyList<LaunchpadTutorial> Tutorials => LaunchpadTutorials.All;
    #endregion

    #region ILaunchpadTutorialHost
    async Task ILaunchpadTutorialHost.AssignFromSoundBankAsync(int pad, string sampleName)
    {
        LaunchpadPadViewModel target = PadAt(pad);
        string path = await _examples.PrepareSampleAsync(sampleName);
        PushUndo();
        target.Assign(path, sampleName, ColumnColors[target.Column]);
        Say($"Pad {pad + 1} now plays '{sampleName}', from the Sound Bank.");
        Changed();
        await PauseAsync(400);
    }

    async Task ILaunchpadTutorialHost.HoldAsync(int pad, int milliseconds)
    {
        LaunchpadPadViewModel target = PadAt(pad);
        PadPressed(target);
        await PauseAsync(milliseconds);
        PadReleased(target);
    }

    async Task ILaunchpadTutorialHost.LoadLessonAsync(string lessonId)
    {
        LaunchpadProject lesson = await _examples.CreateLessonAsync(lessonId);
        ApplyLesson(lesson);
    }

    async Task ILaunchpadTutorialHost.PressAsync(LaunchpadControl control, bool shifted)
    {
        if (control == LaunchpadControl.Track)
        {
            throw new ArgumentException("Track is eight buttons; use PressTrackAsync for the buttons under the pads.", nameof(control));
        }

        LaunchpadKeyViewModel key = AllKeys.First(candidate => candidate.Control == control);
        if (shifted && !IsShiftLatched)
        {
            PressKey(ShiftKey);
            await PauseAsync(ShiftPauseMilliseconds);
        }

        PressKey(key);
        await PauseAsync(PressPauseMilliseconds);
    }

    async Task ILaunchpadTutorialHost.PressTrackAsync(int column)
    {
        PressKey(TrackKeys[column]);
        await PauseAsync(PressPauseMilliseconds);
    }

    Task ILaunchpadTutorialHost.ResetMixerAsync()
    {
        ResetMixer();
        return PauseAsync(200);
    }

    Task ILaunchpadTutorialHost.SetEditModeAsync(bool isOn)
    {
        IsEditMode = isOn;
        return PauseAsync(300);
    }

    Task ILaunchpadTutorialHost.SetScaleAsync(int scaleIndex)
    {
        SetScale(scaleIndex);
        return PauseAsync(200);
    }

    Task ILaunchpadTutorialHost.ShowBankAsync(int bank)
    {
        if (bank != Bank)
        {
            SwitchBank(bank - Bank);
        }

        return PauseAsync(300);
    }

    Task ILaunchpadTutorialHost.StopAllAsync()
    {
        StopAll();
        return PauseAsync(300);
    }

    async Task ILaunchpadTutorialHost.TapAsync(IReadOnlyList<int> pads, int gapMilliseconds)
    {
        foreach (int pad in pads)
        {
            if (pad >= 0)
            {
                PadTapped(PadAt(pad));
            }

            await PauseAsync(gapMilliseconds);
        }
    }

    Task ILaunchpadTutorialHost.ToggleLoopAsync(int pad)
    {
        ToggleLoop(PadAt(pad));
        return PauseAsync(200);
    }

    Task ILaunchpadTutorialHost.WaitAsync(int milliseconds) => PauseAsync(milliseconds);
    #endregion
}
