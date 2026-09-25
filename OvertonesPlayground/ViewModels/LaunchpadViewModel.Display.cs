using System.Globalization;
using OvertonesPlayground.Models;

namespace OvertonesPlayground.ViewModels;

///<summary>
///How the pads and buttons are drawn: which are lit and in what color, and the hint and status lines above the device.
///</summary>
public partial class LaunchpadViewModel
{
    #region Types
    ///<summary>
    ///How one pad is drawn.
    ///</summary>
    private readonly record struct PadLook(string Color, string Caption, bool IsBright, string Name);
    #endregion

    #region Private methods
    ///<summary>
    ///How pad <paramref name="pad"/> looks in the chord layout: the columns are the chord's root going up the scale from an octave
    ///below, and the rows are the chord types.
    ///</summary>
    private PadLook ChordLook(LaunchpadPadViewModel pad)
    {
        int row = pad.Row;
        int column = pad.Column;
        string color = ColumnColors[column];
        int[] tones = ChordTones(row);
        int root = column - 7;
        int scaleLength = LaunchpadScale.Length(_project.ScaleIndex);
        int degreeName = (((root % scaleLength) + scaleLength) % scaleLength) + 1;
        string suffix = ChordSuffix(row);
        string name = $"{ChordName(row)} chord on degree {degreeName}";
        bool hasInstrument = _instrument is not null;
        return new PadLook(hasInstrument ? Lit(color, row % 4 == 3 ? 1 : 0.6) : Dim(color, 0x22), $"{degreeName}{suffix}", hasInstrument && tones.Length > 0, name);
    }

    ///<summary>
    ///Formats a caption number for the pads.
    ///</summary>
    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    ///<summary>
    ///Makes a translucent version of <paramref name="hex"/>, so an unlit pad keeps its column's tint.
    ///</summary>
    private static string Dim(string hex, byte alpha = 0x30) => $"#{alpha:X2}{hex[1..]}";

    ///<summary>
    ///How pad <paramref name="pad"/> looks as part of a vertical fader: lit from the bottom up to <paramref name="value"/>.
    ///</summary>
    private static PadLook FaderLook(LaunchpadPadViewModel pad, double value, string name)
    {
        int level = (int)Math.Round(value * 8);
        bool isLit = 7 - pad.Row < level;
        string color = ColumnColors[pad.Column];
        return new PadLook(isLit ? color : Dim(color, 0x1C), string.Empty, isLit, $"{name}, column {pad.Column + 1}, level {8 - pad.Row} of 8");
    }

    ///<summary>
    ///How pad <paramref name="pad"/> looks as part of a control that shows one lit pad in the column, at <paramref name="level"/>
    ///(0 at the bottom, 7 at the top).
    ///</summary>
    private static PadLook DotLook(LaunchpadPadViewModel pad, int level, string name, Func<int, string> caption)
    {
        bool isLit = 7 - pad.Row == level;
        string color = ColumnColors[pad.Column];
        return new PadLook(isLit ? color : Dim(color, 0x1C), isLit ? caption(level) : string.Empty, isLit, $"{name}, column {pad.Column + 1}, position {8 - pad.Row} of 8");
    }

    ///<summary>
    ///Lights <paramref name="hex"/> at the given brightness (0 to 1).
    ///</summary>
    private static string Lit(string hex, double brightness) => $"#{(byte)Math.Round(255 * Math.Clamp(brightness, 0, 1)):X2}{hex[1..]}";

    ///<summary>
    ///How pad <paramref name="pad"/> looks in the note layout: two rows of scale degrees, repeated up the grid.
    ///</summary>
    private PadLook NoteLook(LaunchpadPadViewModel pad)
    {
        int column = pad.Column;
        string color = ColumnColors[column];
        int degree = NoteDegree(pad.Row, column);
        int semitones = LaunchpadScale.Semitones(_project.ScaleIndex, degree);
        int scaleLength = LaunchpadScale.Length(_project.ScaleIndex);
        int degreeName = (((degree % scaleLength) + scaleLength) % scaleLength) + 1;
        bool isInRange = Math.Abs(semitones) <= MaxNoteSemitones;
        bool isRoot = degreeName == 1;
        bool hasInstrument = _instrument is not null;
        string lit = isRoot ? color : Lit(color, 0.55);
        return new PadLook(isInRange && hasInstrument ? lit : Dim(color, isInRange ? (byte)0x22 : (byte)0x10), isInRange ? Number(degreeName) : string.Empty, isInRange && hasInstrument, isInRange ? $"Note {degreeName}, {semitones:+#;-#;0} semitones" : "Out of range");
    }

    ///<summary>
    ///How pad <paramref name="pad"/> looks while the pattern settings are showing.
    ///</summary>
    private PadLook PatternSettingsLook(LaunchpadPadViewModel pad)
    {
        LaunchpadPattern pattern = CurrentPattern;
        string color = ColumnColors[pad.Column];
        int column = pad.Column;
        string[] directions = ["→", "←", "↔", "?"];
        string[] speeds = ["½×", "1×", "2×"];
        switch (pad.Row)
        {
            case 0:
                int length = (column + 1) * 4;
                return new PadLook(length <= pattern.Length ? color : Dim(color, 0x1C), Number(length), length <= pattern.Length, $"Pattern length {length} steps");
            case 1 when column < directions.Length:
                bool isDirection = (int)pattern.Direction == column;
                return new PadLook(isDirection ? color : Dim(color, 0x1C), directions[column], isDirection, $"Direction {(PatternDirection)column}");
            case 2 when column < speeds.Length:
                bool isSpeed = Math.Abs(pattern.Speed - PatternSpeeds[column]) < 0.01;
                return new PadLook(isSpeed ? color : Dim(color, 0x1C), speeds[column], isSpeed, $"Pattern speed {speeds[column]}");
            default:
                return new PadLook(Dim(color, 0x0C), string.Empty, false, "Unused");
        }
    }

    ///<summary>
    ///How pad <paramref name="pad"/> looks while the patterns are showing: the top row picks one of the eight.
    ///</summary>
    private PadLook PatternsLook(LaunchpadPadViewModel pad)
    {
        string color = ColumnColors[pad.Column];
        if (pad.Row != 0)
        {
            return new PadLook(Dim(color, 0x0C), string.Empty, false, "Unused");
        }

        LaunchpadPattern pattern = _project.Sequence.Patterns[pad.Column];
        bool isSelected = pad.Column == _project.Sequence.SelectedPattern;
        bool hasSteps = pattern.Tracks.Any(track => track.Any(step => step.IsOn));
        string lit = isSelected ? "#FFFFFF" : hasSteps ? color : Dim(color, 0x24);
        return new PadLook(lit, $"P{pad.Column + 1}", isSelected || hasSteps, $"Pattern {pad.Column + 1}{(isSelected ? ", selected" : string.Empty)}{(hasSteps ? ", has steps" : ", empty")}");
    }

    ///<summary>
    ///How pad <paramref name="pad"/> looks in Session and Custom modes: lit in its column's color when it has a sample.
    ///</summary>
    private PadLook SampleLook(LaunchpadPadViewModel pad)
    {
        string color = ColumnColors[pad.Column];
        bool isAudible = IsColumnAudible(pad.Column);
        bool isMarked = ReferenceEquals(_flashedPad, pad) || (_duplicateSource is { } source && ReferenceEquals(source, pad));
        string look;
        if (isMarked)
        {
            look = "#FFFFFF";
        }
        else if (pad.HasClip)
        {
            look = isAudible ? color : Dim(color, 0x55);
        }
        else
        {
            look = Dim(color, 0x24);
        }

        string name = pad.HasClip ? $"Pad {pad.Index + 1}, {pad.Pad.Label}{(pad.IsLooping ? ", looping" : string.Empty)}" : $"Pad {pad.Index + 1}, empty";
        return new PadLook(look, pad.HasClip ? pad.Pad.Label : string.Empty, pad.HasClip || isMarked, name);
    }

    ///<summary>
    ///How pad <paramref name="pad"/> looks in the sequencer. The top four rows are the 32 steps of the selected track, drawn for
    ///the layer being edited; the bottom four rows are the current bank's lower pads, for choosing the track's sample.
    ///</summary>
    private PadLook SequencerLook(LaunchpadPadViewModel pad)
    {
        string color = ColumnColors[pad.Column];
        LaunchpadTrack track = _project.Sequence.Tracks[SelectedTrack];
        if (pad.Row >= StepRows)
        {
            bool isChosen = pad.HasClip && track.HasSource && string.Equals(track.ClipPath, pad.Pad.ClipPath, StringComparison.Ordinal);
            string kit = isChosen ? "#FFFFFF" : pad.HasClip ? color : Dim(color, 0x24);
            return new PadLook(kit, pad.HasClip ? pad.Pad.Label : string.Empty, pad.HasClip, pad.HasClip ? $"Sample {pad.Pad.Label} for track {SelectedTrack + 1}" : "Empty sample slot");
        }

        int index = (pad.Row * Columns) + pad.Column;
        LaunchpadPattern pattern = CurrentPattern;
        LaunchpadStep step = pattern.Tracks[SelectedTrack][index];
        string trackColor = track.HasSource ? track.ColorHex : "#FFFFFF";
        bool isInPattern = index < pattern.Length;
        bool isPlayhead = IsPlaying && index == PlayheadStep;
        string name = $"Step {index + 1}, track {SelectedTrack + 1}, {(step.IsOn ? "on" : "off")}";
        if (!isInPattern)
        {
            return new PadLook(Dim(trackColor, 0x0C), string.Empty, false, $"Step {index + 1}, beyond the pattern length");
        }

        if (isPlayhead)
        {
            return new PadLook("#FFFFFF", string.Empty, true, name);
        }

        if (!step.IsOn)
        {
            return new PadLook(Dim(trackColor, 0x24), string.Empty, false, name);
        }

        return StepLayer switch
        {
            LaunchpadLayer.Velocity => new PadLook(Lit(trackColor, 0.3 + (0.7 * step.Velocity)), Number((int)Math.Round(step.Velocity * 100)), true, $"{name}, velocity {step.Velocity * 100:0}%"),
            LaunchpadLayer.Probability => new PadLook(Lit(trackColor, 0.3 + (0.7 * step.Probability / 100.0)), $"{step.Probability}%", true, $"{name}, probability {step.Probability}%"),
            LaunchpadLayer.MicroStep => new PadLook(trackColor, step.MicroStep == 0 ? "0" : new string('•', step.MicroStep), true, $"{name}, nudged {step.MicroStep} quarters of a step"),
            _ => new PadLook(trackColor, string.Empty, true, name),
        };
    }

    ///<summary>
    ///How pad <paramref name="pad"/> looks while the swing amount is showing: a bar from the left, one column per level.
    ///</summary>
    private PadLook SwingLook(LaunchpadPadViewModel pad)
    {
        string color = ColumnColors[pad.Column];
        bool isLit = pad.Column <= _project.SwingLevel;
        int percent = (int)Math.Round(50 + (pad.Column * 3.5));
        return new PadLook(isLit ? color : Dim(color, 0x1C), pad.Row == 0 ? $"{percent}%" : string.Empty, isLit, $"Swing {percent}%");
    }

    ///<summary>
    ///How pad <paramref name="pad"/> looks while the tempo is showing: one pad per two beats per minute, filled in from the
    ///top left up to the current tempo.
    ///</summary>
    private PadLook TempoLook(LaunchpadPadViewModel pad)
    {
        int bpm = TempoMin + (((pad.Row * Columns) + pad.Column) * TempoStep);
        string color = ColumnColors[pad.Column];
        bool isCurrent = (Math.Abs(bpm - _project.Tempo) < ((TempoStep / 2.0) + 0.01)) || (bpm == _project.Tempo);
        bool isLit = bpm <= _project.Tempo;
        return new PadLook(isCurrent ? "#FFFFFF" : isLit ? color : Dim(color, 0x1C), Number(bpm), isLit, $"Tempo {bpm} BPM");
    }

    ///<summary>
    ///Works out how <paramref name="pad"/> should look for the current mode, layer and tool.
    ///</summary>
    private PadLook LookOf(LaunchpadPadViewModel pad)
    {
        LaunchpadColumn column = _project.Columns[pad.Column];
        switch (Layer)
        {
            case LaunchpadLayer.Volume:
                return FaderLook(pad, column.Volume, "Volume");
            case LaunchpadLayer.Sends:
                return FaderLook(pad, column.Send, "Echo send");
            case LaunchpadLayer.MasterVolume:
                return FaderLook(pad, _project.MasterVolume, "Master volume");
            case LaunchpadLayer.Pan:
                return DotLook(pad, PanLevel(column.Pan), "Pan", level => level is 3 or 4 ? "C" : level < 3 ? "L" : "R");
            case LaunchpadLayer.MasterPan:
                return DotLook(pad, PanLevel(_project.MasterPan), "Master pan", level => level is 3 or 4 ? "C" : level < 3 ? "L" : "R");
            case LaunchpadLayer.Device:
                return DotLook(pad, column.SpeedLevel, "Speed", level => string.Create(CultureInfo.InvariantCulture, $"{SpeedLevels[level]:0.##}×"));
            case LaunchpadLayer.Tempo:
                return TempoLook(pad);
            case LaunchpadLayer.Swing:
                return SwingLook(pad);
            case LaunchpadLayer.Patterns:
                return PatternsLook(pad);
            case LaunchpadLayer.PatternSettings:
                return PatternSettingsLook(pad);
            default:
                break;
        }

        return Mode switch
        {
            LaunchpadMode.Sequencer => SequencerLook(pad),
            LaunchpadMode.Note => NoteLook(pad),
            LaunchpadMode.Chord => ChordLook(pad),
            _ => SampleLook(pad),
        };
    }

    ///<summary>
    ///Which fader position a pan value is nearest to (0 to 7).
    ///</summary>
    private static int PanLevel(double pan) => (int)Math.Round((Math.Clamp(pan, -1, 1) + 1) * 3.5);

    ///<summary>
    ///Redraws every button's light and label state.
    ///</summary>
    private void RefreshKeys()
    {
        foreach (LaunchpadKeyViewModel key in AllKeys)
        {
            key.IsShifted = IsShiftLatched;
            key.IsLit = IsLit(key);
        }

        LaunchpadKeyViewModel play = _keys[LaunchpadControl.Play];
        play.DisplayGlyph = IsPlaying ? IconFont.Stop : IconFont.Play_arrow;
    }

    ///<summary>
    ///Redraws every pad for the current mode, layer and tool, and the hint and status lines.
    ///</summary>
    private void RefreshAll()
    {
        RefreshPads();
        RefreshKeys();
        RefreshTexts();
    }

    ///<summary>
    ///Redraws every pad.
    ///</summary>
    private void RefreshPads()
    {
        foreach (LaunchpadPadViewModel pad in Pads)
        {
            RefreshPad(pad);
        }
    }

    ///<summary>
    ///Redraws one pad.
    ///</summary>
    private void RefreshPad(LaunchpadPadViewModel pad)
    {
        PadLook look = LookOf(pad);
        pad.DisplayColorHex = look.Color;
        pad.Caption = look.Caption;
        pad.CaptionColorHex = look.IsBright ? "#E6101010" : "#CCFFFFFF";
        pad.AccessibleName = look.Name;
        pad.ShowsLoopBadge = ShowsSamples;
    }

    ///<summary>
    ///Whether the pads are showing samples (Session and Custom, with no fader or sequencer layer over them), so they show loop
    ///badges.
    ///</summary>
    private bool ShowsSamples => Mode is LaunchpadMode.Session or LaunchpadMode.Custom && Layer is LaunchpadLayer.None or LaunchpadLayer.RecordArm or LaunchpadLayer.Mute or LaunchpadLayer.Solo or LaunchpadLayer.StopClip;

    ///<summary>
    ///Redraws the hint and status lines.
    ///</summary>
    private void RefreshTexts()
    {
        OnPropertyChanged(nameof(HintText));
        OnPropertyChanged(nameof(StatusText));
    }

    ///<summary>
    ///Which step layer is being edited: Steps unless the sequencer is showing Velocity, Probability or Micro Step.
    ///</summary>
    private LaunchpadLayer StepLayer => Layer is LaunchpadLayer.Velocity or LaunchpadLayer.Probability or LaunchpadLayer.MicroStep ? Layer : LaunchpadLayer.Steps;

    ///<summary>
    ///Builds the line of help above the device for whatever the user is doing.
    ///</summary>
    private string BuildHint()
    {
        if (Tool == LaunchpadTool.Clear)
        {
            return "Clear: tap a pad, step or pattern to clear it. Press Clear again when you're done.";
        }

        if (Tool == LaunchpadTool.Duplicate)
        {
            return _duplicateSource is null && _duplicateStep is null && _duplicatePattern is null
                ? "Duplicate: tap what to copy, then where to put it. Press Duplicate again when you're done."
                : "Duplicate: now tap where to copy it.";
        }

        switch (Layer)
        {
            case LaunchpadLayer.RecordArm:
                return "Record Arm: press the buttons under the pads to arm columns for Capture. With none armed, Capture uses every column.";
            case LaunchpadLayer.Mute:
                return "Mute: press the buttons under the pads to mute or unmute columns.";
            case LaunchpadLayer.Solo:
                return "Solo: press the buttons under the pads to solo columns. While any is soloed, only soloed columns sound.";
            case LaunchpadLayer.StopClip:
                return "Stop Clip: press the buttons under the pads to stop the sounds in a column.";
            case LaunchpadLayer.Volume:
                return "Volume: tap a pad to set the volume of its column.";
            case LaunchpadLayer.Pan:
                return "Pan: tap a pad to set where its column sits, left to right.";
            case LaunchpadLayer.Sends:
                return "Sends: tap a pad to set how much of its column repeats as an echo.";
            case LaunchpadLayer.Device:
                return "Device: tap a pad to set the playback speed (and pitch) of its column.";
            case LaunchpadLayer.MasterVolume:
                return "Master volume: tap a pad to set the volume of everything.";
            case LaunchpadLayer.MasterPan:
                return "Master pan: tap a pad to set where everything sits, left to right.";
            case LaunchpadLayer.Tempo:
                return $"Tempo: tap a pad to set the beats per minute (now {_project.Tempo}).";
            case LaunchpadLayer.Swing:
                return "Swing: tap a pad to set how late every other step falls.";
            case LaunchpadLayer.Patterns:
                return "Patterns: tap a pad on the top row to choose which of the eight patterns is played and edited.";
            case LaunchpadLayer.PatternSettings:
                return "Pattern settings: the top row sets the length, the second the direction and the third the speed.";
            default:
                break;
        }

        return Mode switch
        {
            LaunchpadMode.Session => IsEditMode ? EditHint : PlayHint,
            LaunchpadMode.Note => _instrument is null ? "Note: play a pad in Session mode first, then come back; the pads play that sample as a scale." : $"Note: the pads play '{_instrument.Label}' as a {LaunchpadScale.Name(_project.ScaleIndex).ToLowerInvariant()} scale over two octaves.",
            LaunchpadMode.Chord => _instrument is null ? "Chord: play a pad in Session mode first, then come back; the pads play chords built on that sample." : $"Chord: the pads play chords built on '{_instrument.Label}'. Bottom row triads, then sevenths, sus2 and sus4.",
            LaunchpadMode.Custom => "Custom: each pad plays only while you hold it.",
            LaunchpadMode.Sequencer => $"Sequencer: the top four rows are the steps of track {SelectedTrack + 1}; tap a pad below to give the track a sample, and a button under the pads to change track.",
            _ => PlayHint,
        };
    }

    ///<summary>
    ///Builds the one-line summary of the Launchpad's settings.
    ///</summary>
    private string BuildStatus()
    {
        List<string> parts = [$"Bank {(char)('A' + Bank)}", Mode.ToString(), $"{_project.Tempo} BPM"];
        if (_project.Transpose != 0)
        {
            parts.Add($"Transpose {_project.Transpose:+#;-#;0}");
        }

        if (Mode == LaunchpadMode.Sequencer || IsPlaying)
        {
            parts.Add($"Pattern {_project.Sequence.SelectedPattern + 1}, track {SelectedTrack + 1}");
        }

        if (FixedBeats[_fixedIndex] > 0)
        {
            parts.Add($"Fixed {FixedBeats[_fixedIndex]} beat{(FixedBeats[_fixedIndex] == 1 ? string.Empty : "s")}");
        }

        return string.Join(" • ", parts);
    }
    #endregion

    #region Public properties
    ///<summary>
    ///The line of help above the device, for the mode, layer and tool in use.
    ///</summary>
    public string HintText => BuildHint();

    ///<summary>
    ///The one-line summary of the bank, mode, tempo and other settings.
    ///</summary>
    public string StatusText => BuildStatus();
    #endregion
}
