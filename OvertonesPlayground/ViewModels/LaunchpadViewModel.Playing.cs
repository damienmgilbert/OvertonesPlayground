using OvertonesPlayground.Models;

namespace OvertonesPlayground.ViewModels;

///<summary>
///Making sound and editing pads: playing a pad in each mode, the per-column mixer, the fader layers, and the edit tools.
///</summary>
public partial class LaunchpadViewModel
{
    #region Types
    ///<summary>
    ///One pad hit, remembered so Capture can turn recent playing into a pattern.
    ///</summary>
    private sealed record CapturedHit(string ClipPath, string Label, int Column, string ColorHex, double TimeMs);

    ///<summary>
    ///The sample Note and Chord modes play: the last one played in Session mode.
    ///</summary>
    private sealed record Instrument(string ClipPath, string Label, int Column);
    #endregion

    #region Constants
    ///<summary>
    ///The most a note can be pitched from the sample's own pitch, in semitones either way (an octave halves or doubles the speed).
    ///</summary>
    private const int MaxNoteSemitones = 12;

    ///<summary>
    ///The voice key that sequencer track 0 plays under; track n uses this plus n. Pad voice keys are 0 to 255.
    ///</summary>
    private const int TrackVoiceBase = 1000;
    #endregion

    #region Fields
    private int? _duplicateStep;
    private int? _duplicatePattern;
    private LaunchpadPadViewModel? _duplicateSource;
    private readonly List<CapturedHit> _hits = [];
    private Instrument? _instrument;
    #endregion

    #region Private methods
    [LoggerMessage(Level = LogLevel.Debug, Message = "Voice {VoiceKey} started: volume {Volume:0.00}, balance {Balance:0.00}, speed {Speed:0.00}, loop {Loop}, cut off after {MaxLengthMs:0} ms (0 is never).")]
    private partial void Log_VoiceStarted(int voiceKey, double volume, double balance, double speed, bool loop, double maxLengthMs);

    ///<summary>
    ///Puts an armed edit tool to use on the tapped pad, step or pattern.
    ///</summary>
    private void ApplyTool(LaunchpadPadViewModel pad)
    {
        if (Layer == LaunchpadLayer.Patterns)
        {
            if (pad.Row == 0)
            {
                ApplyPatternTool(pad.Column);
            }

            return;
        }

        if (Mode == LaunchpadMode.Sequencer)
        {
            if (pad.Row < StepRows)
            {
                ApplyStepTool((pad.Row * Columns) + pad.Column);
            }
            else if (Tool == LaunchpadTool.Clear)
            {
                ClearTrackSample();
            }
            else
            {
                Say("Duplicate copies steps and patterns in the sequencer.");
            }

            return;
        }

        if (Tool == LaunchpadTool.Clear)
        {
            if (pad.HasClip)
            {
                ClearPad(pad);
            }
            else
            {
                Say("That pad is already empty.");
            }

            return;
        }

        ApplyPadDuplicate(pad);
    }

    ///<summary>
    ///Duplicate on a pad: the first tap picks the pad to copy, the second the pad to copy it onto.
    ///</summary>
    private void ApplyPadDuplicate(LaunchpadPadViewModel pad)
    {
        if (_duplicateSource is null)
        {
            if (!pad.HasClip)
            {
                Say("That pad is empty; tap a pad with a sample to copy.");
                return;
            }

            _duplicateSource = pad;
            RefreshAll();
            return;
        }

        LaunchpadPadViewModel source = _duplicateSource;
        _duplicateSource = null;
        if (ReferenceEquals(source, pad))
        {
            RefreshAll();
            return;
        }

        PushUndo();
        pad.Assign(source.Pad.ClipPath!, source.Pad.Label, ColumnColors[pad.Column]);
        pad.Pad.Volume = source.Pad.Volume;
        if (pad.IsLooping != source.IsLooping)
        {
            pad.ToggleLoop();
        }

        Say($"Copied '{source.Pad.Label}' to pad {pad.Index + 1}.");
        Changed();
    }

    ///<summary>
    ///Sets a pad column's playback speed level (0 to 7).
    ///</summary>
    private void SetColumnSpeed(int column, int level)
    {
        _project.Columns[column].SpeedLevel = Math.Clamp(level, 0, 7);
        Say($"Column {column + 1} plays at {SpeedLevels[_project.Columns[column].SpeedLevel]:0.##}x speed.");
    }

    ///<summary>
    ///Sets a level from the row tapped in a fader: the row's height, or one step lower if that is already the level, so
    ///the lowest pad can be tapped twice to reach silence.
    ///</summary>
    private static double FaderValue(int row, double current)
    {
        double value = (8 - row) / 8.0;
        return Math.Abs(current - value) < 0.01 ? (7 - row) / 8.0 : value;
    }

    ///<summary>
    ///Lets a layer that turns the pads into controls take a pad tap. Returns false if the current layer doesn't, so the tap goes
    ///to the mode instead.
    ///</summary>
    private bool TryEditLayer(LaunchpadPadViewModel pad)
    {
        LaunchpadColumn column = _project.Columns[pad.Column];
        switch (Layer)
        {
            case LaunchpadLayer.Volume:
                column.Volume = FaderValue(pad.Row, column.Volume);
                Say($"Column {pad.Column + 1} volume {column.Volume * 100:0}%.");
                break;
            case LaunchpadLayer.Sends:
                column.Send = FaderValue(pad.Row, column.Send);
                Say($"Column {pad.Column + 1} echo {column.Send * 100:0}%.");
                break;
            case LaunchpadLayer.MasterVolume:
                _project.MasterVolume = FaderValue(pad.Row, _project.MasterVolume);
                Say($"Master volume {_project.MasterVolume * 100:0}%.");
                break;
            case LaunchpadLayer.Pan:
                column.Pan = ((7 - pad.Row) / 3.5) - 1;
                Say($"Column {pad.Column + 1} pan {column.Pan * 100:+0;-0;0}.");
                break;
            case LaunchpadLayer.MasterPan:
                _project.MasterPan = ((7 - pad.Row) / 3.5) - 1;
                Say($"Master pan {_project.MasterPan * 100:+0;-0;0}.");
                break;
            case LaunchpadLayer.Device:
                SetColumnSpeed(pad.Column, 7 - pad.Row);
                break;
            case LaunchpadLayer.Tempo:
                SetTempo(TempoMin + (((pad.Row * Columns) + pad.Column) * TempoStep));
                Say($"Tempo {_project.Tempo} BPM.");
                break;
            case LaunchpadLayer.Swing:
                _project.SwingLevel = pad.Column;
                Say($"Swing {Math.Round(50 + (pad.Column * 3.5))}%.");
                break;
            case LaunchpadLayer.Patterns:
                if (pad.Row == 0)
                {
                    SelectPattern(pad.Column);
                }

                break;
            case LaunchpadLayer.PatternSettings:
                EditPatternSettings(pad);
                break;
            default:
                return false;
        }

        Changed();
        return true;
    }

    ///<summary>
    ///Chooses which pad a chord's or note's tones sit on, and plays the chord built on that pad's column.
    ///</summary>
    private void PlayChord(LaunchpadPadViewModel pad)
    {
        if (_instrument is not { } instrument)
        {
            Say("Play a pad in Session mode first: Chord plays chords built on the last sample you played.");
            return;
        }

        int root = pad.Column - 7;
        int[] tones = ChordTones(pad.Row);
        foreach (int tone in tones)
        {
            double speed = Math.Pow(2, LaunchpadScale.Semitones(_project.ScaleIndex, root + tone) / 12.0);
            PlayClip(pad.Pad.VoiceKey, instrument.ClipPath, instrument.Column, 0.85 / Math.Sqrt(tones.Length), speed, loop: false, quantise: true);
        }

        FlashPad(pad);
    }

    ///<summary>
    ///The scale degrees, counted up from the chord's root, that sound together in the chord for pad row <paramref name="row"/>.
    ///</summary>
    private static int[] ChordTones(int row) => (row % 4) switch
    {
        3 => [0, 2, 4],
        2 => [0, 2, 4, 6],
        1 => [0, 1, 4],
        _ => [0, 3, 4],
    };

    private static string ChordName(int row) => (row % 4) switch
    {
        3 => "Triad",
        2 => "Seventh",
        1 => "Sus2",
        _ => "Sus4",
    };

    private static string ChordSuffix(int row) => (row % 4) switch
    {
        3 => string.Empty,
        2 => "7",
        1 => "s2",
        _ => "s4",
    };

    ///<summary>
    ///Plays the note under a pad: the last-played sample, pitched by the pad's place in the scale.
    ///</summary>
    private void PlayNote(LaunchpadPadViewModel pad)
    {
        if (_instrument is not { } instrument)
        {
            Say("Play a pad in Session mode first: Note plays the last sample you played as a scale.");
            return;
        }

        int semitones = LaunchpadScale.Semitones(_project.ScaleIndex, NoteDegree(pad.Row, pad.Column));
        if (Math.Abs(semitones) > MaxNoteSemitones)
        {
            Say("That note is out of range.");
            return;
        }

        PlayClip(pad.Pad.VoiceKey, instrument.ClipPath, instrument.Column, 1, Math.Pow(2, semitones / 12.0), loop: false, quantise: true);
        FlashPad(pad);
    }

    ///<summary>
    ///Which scale degree a pad plays in Note mode: two rows make up the two octaves (the lower row from an octave below the root,
    ///the upper row continuing from it), repeated up the grid so the keyboard can be played from anywhere.
    ///</summary>
    private static int NoteDegree(int row, int column) => row % 2 == 1 ? column - 7 : column + 1;

    ///<summary>
    ///Plays whichever thing a tapped pad is in the current mode.
    ///</summary>
    private void PlayPadByMode(LaunchpadPadViewModel pad)
    {
        switch (Mode)
        {
            case LaunchpadMode.Session:
                if (IsEditMode)
                {
                    OpenPadMenu(pad);
                }
                else if (pad.HasClip)
                {
                    PlaySample(pad, live: true);
                }

                break;
            case LaunchpadMode.Custom:
                // Custom pads sound while held (see PadPressed and PadReleased); a tap in edit mode opens the menu as usual.
                if (IsEditMode)
                {
                    OpenPadMenu(pad);
                }

                break;
            case LaunchpadMode.Note:
                PlayNote(pad);
                break;
            case LaunchpadMode.Chord:
                PlayChord(pad);
                break;
            case LaunchpadMode.Sequencer:
                EditSequencer(pad);
                break;
            default:
                break;
        }
    }

    ///<summary>
    ///Starts one sound. Applies everything that shapes it: the column's mute, solo, volume, pan, speed and echo, the master
    ///volume and pan, the transpose, Fixed Length, Radio, and (for pads played live) Quantise.
    ///</summary>
    private void PlayClip(int voiceKey, string clipPath, int column, double velocity, double speedFactor, bool loop, double delayMs = 0, bool quantise = false)
    {
        if (!IsColumnAudible(column))
        {
            return;
        }

        double wait = delayMs + (quantise && _isQuantiseOn ? MsToNextGridLine() : 0);
        if (wait > 1)
        {
            _ = RunAfterAsync(TimeSpan.FromMilliseconds(wait), () => StartVoice(voiceKey, clipPath, column, velocity, speedFactor, loop));
            return;
        }

        StartVoice(voiceKey, clipPath, column, velocity, speedFactor, loop);
    }

    ///<summary>
    ///Runs <paramref name="action"/> after a delay, on the UI thread.
    ///</summary>
    private static async Task RunAfterAsync(TimeSpan delay, Action action)
    {
        await Task.Delay(delay);
        action();
    }

    ///<summary>
    ///Shows a pad lit white for a moment, so a press is visible even when the pad's own color doesn't change.
    ///</summary>
    private void FlashPad(LaunchpadPadViewModel pad)
    {
        _flashedPad = pad;
        RefreshPad(pad);
        _ = ClearFlashAsync(pad);
    }

    private async Task ClearFlashAsync(LaunchpadPadViewModel pad)
    {
        await Task.Delay(FlashTime);
        if (ReferenceEquals(_flashedPad, pad))
        {
            _flashedPad = null;
        }

        RefreshPad(pad);
    }

    ///<summary>
    ///Whether a pad column can currently be heard: not muted, and soloed if any column is.
    ///</summary>
    private bool IsColumnAudible(int column)
    {
        LaunchpadColumn state = _project.Columns[column];
        bool anySoloed = _project.Columns.Any(c => c.IsSoloed);
        return !state.IsMuted && (!anySoloed || state.IsSoloed);
    }

    ///<summary>
    ///Plays a pad's sample: it lights up, is remembered for Note and Chord modes and for Capture (when played live), and is
    ///started.
    ///</summary>
    private void PlaySample(LaunchpadPadViewModel pad, bool live)
    {
        if (!pad.HasClip)
        {
            return;
        }

        FlashPad(pad);
        if (live)
        {
            RememberInstrument(pad);
            RecordHit(pad);
        }

        PlayClip(pad.Pad.VoiceKey, pad.Pad.ClipPath!, pad.Column, pad.Pad.Volume, 1, pad.Pad.IsLooping, quantise: live);
    }

    ///<summary>
    ///Remembers a hit, if its column is armed (or none is), so Capture can turn recent playing into a pattern.
    ///</summary>
    private void RecordHit(LaunchpadPadViewModel pad)
    {
        bool anyArmed = _project.Columns.Any(column => column.IsArmed);
        if (anyArmed && !_project.Columns[pad.Column].IsArmed)
        {
            return;
        }

        bool wasEmpty = _hits.Count == 0;
        double now = _clock.Elapsed.TotalMilliseconds;
        _hits.Add(new CapturedHit(pad.Pad.ClipPath!, pad.Pad.Label, pad.Column, ColumnColors[pad.Column], now));
        _hits.RemoveAll(hit => now - hit.TimeMs > CaptureMemoryMs);
        if (wasEmpty)
        {
            RefreshKeys();
        }
    }

    ///<summary>
    ///Makes a played pad's sample the one Note and Chord modes play.
    ///</summary>
    private void RememberInstrument(LaunchpadPadViewModel pad)
    {
        Instrument next = new(pad.Pad.ClipPath!, pad.Pad.Label, pad.Column);
        if (_instrument != next)
        {
            _instrument = next;
            RefreshAll();
        }
    }

    ///<summary>
    ///Starts one sound, with everything that shapes it applied.
    ///</summary>
    private void StartVoice(int voiceKey, string clipPath, int column, double velocity, double speedFactor, bool loop)
    {
        LaunchpadColumn state = _project.Columns[column];
        if (_isRadioOn && voiceKey < TrackVoiceBase)
        {
            for (int row = 0; row < Rows; row++)
            {
                int other = (voiceKey / LaunchpadProject.PadsPerBank * LaunchpadProject.PadsPerBank) + (row * Columns) + column;
                if (other != voiceKey)
                {
                    _playbackService.StopPad(other);
                }
            }
        }

        double volume = velocity * state.Volume * _project.MasterVolume;
        double balance = Math.Clamp(state.Pan + _project.MasterPan, -1, 1);
        double speed = SpeedLevels[state.SpeedLevel] * Math.Pow(2, _project.Transpose / 12.0) * speedFactor;
        TimeSpan? maxLength = FixedBeats[_fixedIndex] > 0 ? TimeSpan.FromMilliseconds(FixedBeats[_fixedIndex] * 60000.0 / _project.Tempo) : null;
        Log_VoiceStarted(voiceKey, volume, balance, speed, loop, maxLength?.TotalMilliseconds ?? 0);
        _playbackService.TriggerVoice(voiceKey, clipPath, new PadVoiceOptions(volume, balance, speed, loop, maxLength));

        // Sends: a few quieter repeats an eighth note apart.
        int repeats = (int)Math.Ceiling(state.Send * 4);
        double eighthMs = 30000.0 / _project.Tempo;
        for (int repeat = 1; repeat <= repeats; repeat++)
        {
            double echoVolume = volume * Math.Pow(0.6, repeat);
            _ = RunAfterAsync(TimeSpan.FromMilliseconds(eighthMs * repeat), () => _playbackService.TriggerVoice(voiceKey, clipPath, new PadVoiceOptions(echoVolume, balance, speed, false, maxLength)));
        }
    }
    #endregion
}
