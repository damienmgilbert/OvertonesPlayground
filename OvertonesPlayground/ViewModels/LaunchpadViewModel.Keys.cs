using OvertonesPlayground.Models;

namespace OvertonesPlayground.ViewModels;

///<summary>
///The ring of buttons around the pads: building them, lighting them, and what each does when pressed.
///</summary>
public partial class LaunchpadViewModel
{
    #region Constants
    ///<summary>
    ///How many beats each Fixed Length setting cuts a sound off after; 0 is off.
    ///</summary>
    private static readonly int[] FixedBeats = [0, 1, 2, 4, 8];

    ///<summary>
    ///How long a message stays in the status line before it clears.
    ///</summary>
    private static readonly TimeSpan MessageTime = TimeSpan.FromSeconds(4);

    ///<summary>
    ///The eight playback speeds the Device layer chooses between, bottom pad to top pad.
    ///</summary>
    private static readonly double[] SpeedLevels = [0.5, 0.6, 0.75, 1.0, 1.25, 1.5, 1.75, 2.0];
    #endregion

    #region Fields
    private readonly Dictionary<LaunchpadControl, LaunchpadKeyViewModel> _keys = [];
    private int _fixedIndex;
    private bool _isClickOn;
    private bool _isQuantiseOn;
    private bool _isRadioOn;
    private bool _isRecordQuantiseOn;
    private LaunchpadControl? _flashedKey;
    private readonly List<DateTime> _tapTimes = [];
    #endregion

    #region Private methods
    ///<summary>
    ///Makes a key and remembers it by its control (track keys are kept in <see cref="TrackKeys"/> instead).
    ///</summary>
    private LaunchpadKeyViewModel Key(LaunchpadControl control, string id, string label, string? shiftLabel, string? glyph, string description, int position, int column = 0)
    {
        LaunchpadKeyViewModel key = new(control, id, label, shiftLabel, glyph, description, position, column);
        if (control != LaunchpadControl.Track)
        {
            _keys[control] = key;
        }

        return key;
    }

    ///<summary>
    ///Creates every button in the ring: the top row, the two side columns, the track buttons, and the function buttons, in the
    ///order they sit on the hardware.
    ///</summary>
    private void BuildKeys()
    {
        ShiftKey = Key(LaunchpadControl.Shift, "shift", "Shift", null, null, "Shift. Press it, then another button, to run that button's second function (the small label under its name).", 0);
        SetupKey = Key(LaunchpadControl.Setup, "setup", "Setup", null, null, "Setup. Choose the scale for Note and Chord modes, reset the mixer, or clear a bank or the sequencer.", 0);

        TopKeys =
        [
            Key(LaunchpadControl.Left, "left", string.Empty, null, IconFont.Arrow_left, "Previous bank. The pads come in four banks, A to D.", 0),
            Key(LaunchpadControl.Right, "right", string.Empty, null, IconFont.Arrow_right, "Next bank. The pads come in four banks, A to D.", 1),
            Key(LaunchpadControl.Session, "session", "Session", null, null, "Session mode. Each pad launches its own sample.", 2),
            Key(LaunchpadControl.Note, "note", "Note", null, null, "Note mode. The pads play the last sample you played as a scale over two octaves.", 3),
            Key(LaunchpadControl.Chord, "chord", "Chord", null, null, "Chord mode. The pads play chords built on the last sample you played.", 4),
            Key(LaunchpadControl.Custom, "custom", "Custom", null, null, "Custom mode. Each pad plays its sample only while you hold it.", 5),
            Key(LaunchpadControl.Sequencer, "sequencer", "Sequencer", null, null, "Sequencer mode. A four-track, 32-step sequencer: the pads are the steps.", 6),
            Key(LaunchpadControl.Projects, "projects", "Projects", "Save", null, "Projects. Save, open or start a project. With Shift, Save saves the current one straight away.", 7),
        ];

        LeftKeys =
        [
            Key(LaunchpadControl.Up, "up", string.Empty, null, IconFont.Arrow_drop_up, "Transpose up a semitone. Changes the pitch of every pad, and of the sequencer.", 0),
            Key(LaunchpadControl.Down, "down", string.Empty, null, IconFont.Arrow_drop_down, "Transpose down a semitone. Changes the pitch of every pad, and of the sequencer.", 1),
            Key(LaunchpadControl.Clear, "clear", "Clear", null, null, "Clear. Then tap a pad, step or pattern to clear it. Press it again when you're done.", 2),
            Key(LaunchpadControl.Duplicate, "duplicate", "Duplicate", "Double", null, "Duplicate. Tap a pad, step or pattern, then where to copy it. With Shift, Double repeats the pattern to fill twice its length.", 3),
            Key(LaunchpadControl.Quantise, "quantise", "Quantise", "Record Quantise", null, "Quantise. Snaps the pads you play to the tempo grid. With Shift, Record Quantise snaps Capture to whole steps.", 4),
            Key(LaunchpadControl.FixedLength, "fixed-length", "Fixed Length", null, null, "Fixed length. Cuts every sound off after one beat, two, one bar, two bars, or leaves it whole.", 5),
            Key(LaunchpadControl.Play, "play", string.Empty, null, IconFont.Play_arrow, "Play. Starts and stops the sequencer.", 6),
            Key(LaunchpadControl.Capture, "capture", "Capture", null, IconFont.Radio_button_unchecked, "Capture. Turns the last two bars of pads you played into a sequencer pattern.", 7),
        ];

        RightKeys =
        [
            Key(LaunchpadControl.Patterns, "patterns", "Patterns", null, IconFont.Chevron_right, "Patterns. Choose which of the eight patterns is played and edited.", 0),
            Key(LaunchpadControl.Steps, "steps", "Steps", null, IconFont.Chevron_right, "Steps. Turn the sequencer's steps on and off.", 1),
            Key(LaunchpadControl.PatternSettings, "pattern-settings", "Pattern Settings", null, IconFont.Chevron_right, "Pattern settings. Set the pattern's length, direction and speed.", 2),
            Key(LaunchpadControl.Velocity, "velocity", "Velocity", null, IconFont.Chevron_right, "Velocity. Set how loud each step is.", 3),
            Key(LaunchpadControl.Probability, "probability", "Probability", null, IconFont.Chevron_right, "Probability. Set how likely each step is to sound.", 4),
            Key(LaunchpadControl.Mutation, "mutation", "Mutation", null, IconFont.Chevron_right, "Mutation. Randomly changes the pattern: some steps flip, some get louder or quieter.", 5),
            Key(LaunchpadControl.MicroStep, "micro-step", "Micro Step", null, IconFont.Chevron_right, "Micro step. Nudge each step late by a quarter, a half or three quarters of a step.", 6),
            Key(LaunchpadControl.PrintToClip, "print-to-clip", "Print to Clip", null, IconFont.Chevron_right, "Print to clip. Renders the pattern to a new audio clip in your library.", 7),
        ];

        TrackKeys = [.. Enumerable.Range(0, LaunchpadProject.ColumnCount).Select(column => Key(LaunchpadControl.Track, $"track-{column + 1}", "—", null, null, "Acts on this pad column: launches it, or does what the function button below has chosen. In the sequencer, chooses the track.", column, column))];

        FunctionKeys =
        [
            Key(LaunchpadControl.RecordArm, "record-arm", "Record Arm", "Undo", null, "Record Arm. Arm columns, with the buttons above, for Capture. With Shift, Undo undoes the last edit.", 0),
            Key(LaunchpadControl.Mute, "mute", "Mute", "Radio", null, "Mute. Mute columns with the buttons above. With Shift, Radio lets only one pad in a column sound at a time.", 1),
            Key(LaunchpadControl.Solo, "solo", "Solo", "Click", null, "Solo. Solo columns with the buttons above. With Shift, Click turns the metronome on and off.", 2),
            Key(LaunchpadControl.Volume, "volume", "Volume", "•", null, "Volume. The pads become one volume fader per column. With Shift, one master volume fader.", 3),
            Key(LaunchpadControl.Pan, "pan", "Pan", "••", null, "Pan. The pads set each column's left to right position. With Shift, the master pan.", 4),
            Key(LaunchpadControl.Sends, "sends", "Sends", "Tap", null, "Sends. The pads set how much of each column repeats as an echo. With Shift, Tap sets the tempo as you tap.", 5),
            Key(LaunchpadControl.Device, "device", "Device", "Tempo", null, "Device. The pads set each column's playback speed and pitch. With Shift, choose the tempo.", 6),
            Key(LaunchpadControl.StopClip, "stop-clip", "Stop Clip", "Swing", null, "Stop Clip. Stop the sounds in a column with the buttons above. With Shift, choose the swing.", 7),
        ];
    }

    ///<summary>
    ///Lets a value on a key or pad show for a moment, for buttons whose effect isn't otherwise visible.
    ///</summary>
    private async Task FlashKeyAsync(LaunchpadControl control)
    {
        _flashedKey = control;
        RefreshKeys();
        await Task.Delay(FlashTime);
        _flashedKey = null;
        RefreshKeys();
    }

    ///<summary>
    ///Whether the light behind <paramref name="key"/> is on.
    ///</summary>
    private bool IsLit(LaunchpadKeyViewModel key) => key.Control switch
    {
        LaunchpadControl.Shift => IsShiftLatched,
        LaunchpadControl.Session => Mode == LaunchpadMode.Session,
        LaunchpadControl.Note => Mode == LaunchpadMode.Note,
        LaunchpadControl.Chord => Mode == LaunchpadMode.Chord,
        LaunchpadControl.Custom => Mode == LaunchpadMode.Custom,
        LaunchpadControl.Sequencer => Mode == LaunchpadMode.Sequencer,
        LaunchpadControl.Up => _project.Transpose > 0,
        LaunchpadControl.Down => _project.Transpose < 0,
        LaunchpadControl.Clear => Tool == LaunchpadTool.Clear,
        LaunchpadControl.Duplicate => Tool == LaunchpadTool.Duplicate,
        LaunchpadControl.Quantise => _isQuantiseOn || _isRecordQuantiseOn,
        LaunchpadControl.FixedLength => _fixedIndex > 0,
        LaunchpadControl.Play => IsPlaying,
        LaunchpadControl.Capture => _hits.Count > 0,
        LaunchpadControl.Patterns => Layer == LaunchpadLayer.Patterns,
        LaunchpadControl.Steps => Mode == LaunchpadMode.Sequencer && Layer is LaunchpadLayer.None or LaunchpadLayer.Steps,
        LaunchpadControl.PatternSettings => Layer == LaunchpadLayer.PatternSettings,
        LaunchpadControl.Velocity => Layer == LaunchpadLayer.Velocity,
        LaunchpadControl.Probability => Layer == LaunchpadLayer.Probability,
        LaunchpadControl.MicroStep => Layer == LaunchpadLayer.MicroStep,
        LaunchpadControl.Mutation or LaunchpadControl.PrintToClip or LaunchpadControl.Projects => _flashedKey == key.Control,
        LaunchpadControl.Track => IsTrackKeyLit(key.Column),
        LaunchpadControl.RecordArm => Layer == LaunchpadLayer.RecordArm,
        LaunchpadControl.Mute => Layer == LaunchpadLayer.Mute || _isRadioOn,
        LaunchpadControl.Solo => Layer == LaunchpadLayer.Solo || _isClickOn,
        LaunchpadControl.Volume => Layer is LaunchpadLayer.Volume or LaunchpadLayer.MasterVolume,
        LaunchpadControl.Pan => Layer is LaunchpadLayer.Pan or LaunchpadLayer.MasterPan,
        LaunchpadControl.Sends => Layer == LaunchpadLayer.Sends,
        LaunchpadControl.Device => Layer is LaunchpadLayer.Device or LaunchpadLayer.Tempo,
        LaunchpadControl.StopClip => Layer is LaunchpadLayer.StopClip or LaunchpadLayer.Swing,
        _ => false,
    };

    ///<summary>
    ///Whether the track button for pad column <paramref name="column"/> is lit: the selected track in the sequencer, or the
    ///column's armed, muted or soloed state.
    ///</summary>
    private bool IsTrackKeyLit(int column)
    {
        if (IsSequencerTrackSelection)
        {
            return column == SelectedTrack;
        }

        LaunchpadColumn state = _project.Columns[column];
        return Layer switch
        {
            LaunchpadLayer.RecordArm => state.IsArmed,
            LaunchpadLayer.Mute => state.IsMuted,
            LaunchpadLayer.Solo => state.IsSoloed,
            _ => false,
        };
    }

    ///<summary>
    ///Whether the track buttons currently choose the sequencer's track (true) or act on pad columns (false).
    ///</summary>
    private bool IsSequencerTrackSelection => Mode == LaunchpadMode.Sequencer && Layer is LaunchpadLayer.None or LaunchpadLayer.Patterns or LaunchpadLayer.Steps or LaunchpadLayer.PatternSettings or LaunchpadLayer.Velocity or LaunchpadLayer.Probability or LaunchpadLayer.MicroStep;

    ///<summary>
    ///Puts a message in the status line, and clears it again after a few seconds unless something newer has replaced it.
    ///</summary>
    private void Say(string message)
    {
        StatusMessage = message;
        _ = ClearMessageLaterAsync(message);
    }

    private async Task ClearMessageLaterAsync(string message)
    {
        await Task.Delay(MessageTime);
        if (string.Equals(StatusMessage, message, StringComparison.Ordinal))
        {
            StatusMessage = null;
        }
    }

    ///<summary>
    ///Drops any half-finished edit tool, so a tool armed earlier can't catch a tap meant for something else.
    ///</summary>
    private void ClearTool()
    {
        _duplicatePattern = null;
        _duplicateSource = null;
        _duplicateStep = null;
        Tool = LaunchpadTool.None;
    }

    ///<summary>
    ///Runs what a button does. The caller has already worked out whether Shift was latched.
    ///</summary>
    private void Dispatch(LaunchpadKeyViewModel key, bool isShifted)
    {
        switch (key.Control)
        {
            case LaunchpadControl.Shift:
                IsShiftLatched = !IsShiftLatched;
                break;
            case LaunchpadControl.Left:
                SwitchBank(-1);
                break;
            case LaunchpadControl.Right:
                SwitchBank(1);
                break;
            case LaunchpadControl.Session:
                SetMode(LaunchpadMode.Session);
                break;
            case LaunchpadControl.Note:
                SetMode(LaunchpadMode.Note);
                break;
            case LaunchpadControl.Chord:
                SetMode(LaunchpadMode.Chord);
                break;
            case LaunchpadControl.Custom:
                SetMode(LaunchpadMode.Custom);
                break;
            case LaunchpadControl.Sequencer:
                SetMode(LaunchpadMode.Sequencer);
                break;
            case LaunchpadControl.Projects:
                if (isShifted)
                {
                    _ = SaveProjectAsync(askForName: false);
                }
                else
                {
                    OpenProjectsMenu();
                }

                break;
            case LaunchpadControl.Up:
                Transpose(1);
                break;
            case LaunchpadControl.Down:
                Transpose(-1);
                break;
            case LaunchpadControl.Clear:
                ToggleTool(LaunchpadTool.Clear);
                break;
            case LaunchpadControl.Duplicate:
                if (isShifted)
                {
                    DoublePattern();
                }
                else
                {
                    ToggleTool(LaunchpadTool.Duplicate);
                }

                break;
            case LaunchpadControl.Quantise:
                ToggleQuantise(isShifted);
                break;
            case LaunchpadControl.FixedLength:
                CycleFixedLength();
                break;
            case LaunchpadControl.Play:
                TogglePlay();
                break;
            case LaunchpadControl.Capture:
                Capture();
                break;
            case LaunchpadControl.Patterns:
                SelectSequencerLayer(LaunchpadLayer.Patterns);
                break;
            case LaunchpadControl.Steps:
                SelectSequencerLayer(LaunchpadLayer.Steps);
                break;
            case LaunchpadControl.PatternSettings:
                SelectSequencerLayer(LaunchpadLayer.PatternSettings);
                break;
            case LaunchpadControl.Velocity:
                SelectSequencerLayer(LaunchpadLayer.Velocity);
                break;
            case LaunchpadControl.Probability:
                SelectSequencerLayer(LaunchpadLayer.Probability);
                break;
            case LaunchpadControl.MicroStep:
                SelectSequencerLayer(LaunchpadLayer.MicroStep);
                break;
            case LaunchpadControl.Mutation:
                Mutate();
                break;
            case LaunchpadControl.PrintToClip:
                _ = PrintToClipAsync();
                break;
            case LaunchpadControl.Track:
                PressTrack(key.Column);
                break;
            case LaunchpadControl.RecordArm:
                if (isShifted)
                {
                    Undo();
                }
                else
                {
                    ToggleLayer(LaunchpadLayer.RecordArm);
                }

                break;
            case LaunchpadControl.Mute:
                if (isShifted)
                {
                    ToggleRadio();
                }
                else
                {
                    ToggleLayer(LaunchpadLayer.Mute);
                }

                break;
            case LaunchpadControl.Solo:
                if (isShifted)
                {
                    ToggleClick();
                }
                else
                {
                    ToggleLayer(LaunchpadLayer.Solo);
                }

                break;
            case LaunchpadControl.Volume:
                ToggleLayer(isShifted ? LaunchpadLayer.MasterVolume : LaunchpadLayer.Volume);
                break;
            case LaunchpadControl.Pan:
                ToggleLayer(isShifted ? LaunchpadLayer.MasterPan : LaunchpadLayer.Pan);
                break;
            case LaunchpadControl.Sends:
                if (isShifted)
                {
                    TapTempo();
                }
                else
                {
                    ToggleLayer(LaunchpadLayer.Sends);
                }

                break;
            case LaunchpadControl.Device:
                ToggleLayer(isShifted ? LaunchpadLayer.Tempo : LaunchpadLayer.Device);
                break;
            case LaunchpadControl.StopClip:
                ToggleLayer(isShifted ? LaunchpadLayer.Swing : LaunchpadLayer.StopClip);
                break;
            case LaunchpadControl.Setup:
                OpenSetupMenu();
                break;
            default:
                break;
        }
    }

    ///<summary>
    ///Turns Fixed Length to its next setting: off, one beat, two, a bar, two bars, and round again.
    ///</summary>
    private void CycleFixedLength()
    {
        _fixedIndex = (_fixedIndex + 1) % FixedBeats.Length;
        int beats = FixedBeats[_fixedIndex];
        Say(beats == 0 ? "Fixed length off: sounds play to the end." : $"Fixed length: sounds are cut off after {beats} beat{(beats == 1 ? string.Empty : "s")}.");
    }

    ///<summary>
    ///Launches every sample in one pad column at once.
    ///</summary>
    private void LaunchColumn(int column)
    {
        int launched = 0;
        foreach (LaunchpadPadViewModel pad in Pads.Where(p => p.Column == column && p.HasClip))
        {
            PlaySample(pad, live: true);
            launched++;
        }

        Say(launched == 0 ? $"Column {column + 1} has no samples." : $"Launched column {column + 1}.");
    }

    ///<summary>
    ///Presses one of the buttons under the pads. What it does depends on the layer: choose the sequencer track, arm, mute or
    ///solo the column, stop the column, or (with no layer) launch the column.
    ///</summary>
    private void PressTrack(int column)
    {
        if (IsSequencerTrackSelection)
        {
            if (column < LaunchpadPattern.TrackCount)
            {
                SelectedTrack = column;
                Say($"Track {column + 1}: {(_project.Sequence.Tracks[column].HasSource ? _project.Sequence.Tracks[column].Label : "no sample yet")}.");
            }
            else
            {
                Say("The sequencer has four tracks; use the first four buttons.");
            }

            return;
        }

        LaunchpadColumn state = _project.Columns[column];
        switch (Layer)
        {
            case LaunchpadLayer.RecordArm:
                state.IsArmed = !state.IsArmed;
                Say($"Column {column + 1} {(state.IsArmed ? "armed" : "disarmed")} for Capture.");
                Changed();
                break;
            case LaunchpadLayer.Mute:
                state.IsMuted = !state.IsMuted;
                if (state.IsMuted)
                {
                    StopColumn(column);
                }

                Say($"Column {column + 1} {(state.IsMuted ? "muted" : "unmuted")}.");
                Changed();
                break;
            case LaunchpadLayer.Solo:
                state.IsSoloed = !state.IsSoloed;
                Say($"Column {column + 1} {(state.IsSoloed ? "soloed" : "unsoloed")}.");
                Changed();
                break;
            case LaunchpadLayer.StopClip:
                StopColumn(column);
                Say($"Stopped column {column + 1}.");
                break;
            case LaunchpadLayer.None when Mode is LaunchpadMode.Session or LaunchpadMode.Custom:
                LaunchColumn(column);
                break;
            default:
                Say("Tap the pads to set the values; the buttons under them are for the column functions.");
                break;
        }
    }

    ///<summary>
    ///Shows one sequencer layer (Patterns, Steps, Pattern Settings, Velocity, Probability or Micro Step), switching to the
    ///sequencer if it isn't showing. Pressing the layer again goes back to plain steps.
    ///</summary>
    private void SelectSequencerLayer(LaunchpadLayer layer)
    {
        ClearTool();
        Mode = LaunchpadMode.Sequencer;
        Layer = Layer == layer && layer != LaunchpadLayer.Steps ? LaunchpadLayer.None : layer;
    }

    ///<summary>
    ///Chooses what the pads do, and returns to the plain mode (no layer, no tool).
    ///</summary>
    private void SetMode(LaunchpadMode mode)
    {
        ClearTool();
        Layer = LaunchpadLayer.None;
        Mode = mode;
        if (mode is LaunchpadMode.Note or LaunchpadMode.Chord && _instrument is null)
        {
            Say("Play a pad in Session mode first: Note and Chord play the last sample you played.");
        }
    }

    ///<summary>
    ///Stops every sound in one pad column, in every bank.
    ///</summary>
    private void StopColumn(int column)
    {
        for (int bank = 0; bank < LaunchpadProject.BankCount; bank++)
        {
            for (int row = 0; row < Rows; row++)
            {
                _playbackService.StopPad((bank * LaunchpadProject.PadsPerBank) + (row * Columns) + column);
            }
        }
    }

    ///<summary>
    ///Shows the previous or next pad bank.
    ///</summary>
    private void SwitchBank(int step)
    {
        int next = (Bank + step + LaunchpadProject.BankCount) % LaunchpadProject.BankCount;
        _duplicateSource = null;
        Bank = next;
        _project.Bank = next;
        for (int i = 0; i < Pads.Count; i++)
        {
            Pads[i].Bind(_banks[next][i]);
        }

        Say($"Bank {(char)('A' + next)}.");
        Changed();
    }

    ///<summary>
    ///Arms or disarms an edit tool. Only one can be armed.
    ///</summary>
    private void ToggleTool(LaunchpadTool tool)
    {
        bool isSame = Tool == tool;
        ClearTool();
        if (!isSame)
        {
            Layer = Layer is LaunchpadLayer.Patterns or LaunchpadLayer.Steps or LaunchpadLayer.Velocity or LaunchpadLayer.Probability or LaunchpadLayer.MicroStep or LaunchpadLayer.None or LaunchpadLayer.RecordArm or LaunchpadLayer.Mute or LaunchpadLayer.Solo or LaunchpadLayer.StopClip ? Layer : LaunchpadLayer.None;
            Tool = tool;
        }
    }

    ///<summary>
    ///Shows one layer over the mode, or goes back to the plain mode if that layer is already showing.
    ///</summary>
    private void ToggleLayer(LaunchpadLayer layer)
    {
        ClearTool();
        Layer = Layer == layer ? LaunchpadLayer.None : layer;
    }

    ///<summary>
    ///Turns the metronome on or off.
    ///</summary>
    private void ToggleClick()
    {
        _isClickOn = !_isClickOn;
        Say(_isClickOn ? "Click on." : "Click off.");
        if (_isClickOn)
        {
            _ = StartClickAsync();
        }
        else
        {
            StopClick();
        }
    }

    ///<summary>
    ///Turns quantise on or off: for live pads, or (shifted) for Capture.
    ///</summary>
    private void ToggleQuantise(bool isShifted)
    {
        if (isShifted)
        {
            _isRecordQuantiseOn = !_isRecordQuantiseOn;
            Say(_isRecordQuantiseOn ? "Record quantise on: Capture snaps your hits to whole steps." : "Record quantise off: Capture keeps your timing to a quarter of a step.");
        }
        else
        {
            _isQuantiseOn = !_isQuantiseOn;
            Say(_isQuantiseOn ? "Quantise on: pads you play wait for the next sixteenth note." : "Quantise off.");
        }
    }

    ///<summary>
    ///Turns radio mode on or off: with it on, playing a pad stops the others in its column.
    ///</summary>
    private void ToggleRadio()
    {
        _isRadioOn = !_isRadioOn;
        Say(_isRadioOn ? "Radio on: playing a pad stops the others in its column." : "Radio off.");
        Changed();
    }

    ///<summary>
    ///Sets the tempo from how fast the button is being tapped.
    ///</summary>
    private void TapTempo()
    {
        DateTime now = DateTime.UtcNow;
        if (_tapTimes.Count > 0 && (now - _tapTimes[^1]).TotalSeconds > 2)
        {
            _tapTimes.Clear();
        }

        _tapTimes.Add(now);
        if (_tapTimes.Count > 5)
        {
            _tapTimes.RemoveAt(0);
        }

        if (_tapTimes.Count < 2)
        {
            Say("Tap again to set the tempo.");
            return;
        }

        double averageSeconds = (_tapTimes[^1] - _tapTimes[0]).TotalSeconds / (_tapTimes.Count - 1);
        SetTempo((int)Math.Round(60 / averageSeconds));
        Say($"Tempo {_project.Tempo} BPM.");
    }

    ///<summary>
    ///Transposes everything by <paramref name="semitones"/>, within an octave either way.
    ///</summary>
    private void Transpose(int semitones)
    {
        _project.Transpose = Math.Clamp(_project.Transpose + semitones, -12, 12);
        Say($"Transpose {_project.Transpose:+#;-#;0} semitones.");
        Changed();
    }

    ///<summary>
    ///Undoes the last edit to the pads or the sequencer.
    ///</summary>
    private void Undo()
    {
        if (!TryUndo())
        {
            Say("Nothing to undo.");
        }
        else
        {
            Say("Undone.");
        }
    }
    #endregion

    #region Public methods
    ///<summary>
    ///A button was pressed. Shift, if latched, applies to this press only and is then released.
    ///</summary>
    public void PressKey(LaunchpadKeyViewModel? key)
    {
        if (key is null)
        {
            return;
        }

        bool isShifted = IsShiftLatched && key.Control != LaunchpadControl.Shift;
        Log_KeyPressed(key.Control, isShifted);
        try
        {
            Dispatch(key, isShifted);
        }
        finally
        {
            if (key.Control != LaunchpadControl.Shift && IsShiftLatched)
            {
                IsShiftLatched = false;
            }

            RefreshAll();
        }
    }
    #endregion

    #region Public properties
    ///<summary>
    ///Every button in the ring, for lighting them.
    ///</summary>
    public IEnumerable<LaunchpadKeyViewModel> AllKeys => [ShiftKey, SetupKey, .. TopKeys, .. LeftKeys, .. RightKeys, .. TrackKeys, .. FunctionKeys];

    ///<summary>
    ///The bottom row of labeled buttons: the column functions and their shifted alternatives.
    ///</summary>
    public IReadOnlyList<LaunchpadKeyViewModel> FunctionKeys { get; private set; } = [];

    ///<summary>
    ///The left-hand buttons, top to bottom: transpose, the edit tools, transport and capture.
    ///</summary>
    public IReadOnlyList<LaunchpadKeyViewModel> LeftKeys { get; private set; } = [];

    ///<summary>
    ///The right-hand buttons, top to bottom: the sequencer's layers and actions.
    ///</summary>
    public IReadOnlyList<LaunchpadKeyViewModel> RightKeys { get; private set; } = [];

    ///<summary>
    ///The Shift button, at the top left.
    ///</summary>
    public LaunchpadKeyViewModel ShiftKey { get; private set; } = null!;

    ///<summary>
    ///The Setup button, at the bottom left.
    ///</summary>
    public LaunchpadKeyViewModel SetupKey { get; private set; } = null!;

    ///<summary>
    ///The Setup button as a list of one, for the page's item layout.
    ///</summary>
    public IReadOnlyList<LaunchpadKeyViewModel> SetupKeys => [SetupKey];

    ///<summary>
    ///The Shift button as a list of one, for the page's item layout.
    ///</summary>
    public IReadOnlyList<LaunchpadKeyViewModel> ShiftKeys => [ShiftKey];

    ///<summary>
    ///The top row of buttons: bank arrows, the modes and Projects.
    ///</summary>
    public IReadOnlyList<LaunchpadKeyViewModel> TopKeys { get; private set; } = [];

    ///<summary>
    ///The row of eight buttons directly under the pads.
    ///</summary>
    public IReadOnlyList<LaunchpadKeyViewModel> TrackKeys { get; private set; } = [];
    #endregion
}
