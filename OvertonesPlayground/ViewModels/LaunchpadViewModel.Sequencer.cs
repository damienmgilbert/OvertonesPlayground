using OvertonesPlayground.Models;

namespace OvertonesPlayground.ViewModels;

///<summary>
///The step sequencer and everything that keeps time: the transport, the metronome, Quantise's grid, step editing, Mutation,
///Capture and Print to Clip.
///</summary>
public partial class LaunchpadViewModel
{
    #region Constants
    ///<summary>
    ///How long, in milliseconds, played pads are remembered for Capture.
    ///</summary>
    private const double CaptureMemoryMs = 60000;

    ///<summary>
    ///The voice key the metronome plays under.
    ///</summary>
    private const int ClickVoiceKey = 2000;

    ///<summary>
    ///How many rows of pads are steps in the sequencer; the rows below choose the track's sample.
    ///</summary>
    private const int StepRows = 4;

    ///<summary>
    ///The tempo of the first pad in the tempo layer, in beats per minute.
    ///</summary>
    private const int TempoMin = 60;

    ///<summary>
    ///How many beats per minute apart the pads in the tempo layer are.
    ///</summary>
    private const int TempoStep = 2;

    private static readonly double[] PatternSpeeds = [0.5, 1, 2];
    private static readonly int[] ProbabilityLevels = [100, 75, 50, 25];
    private static readonly double[] VelocityLevels = [1, 0.75, 0.5, 0.25];
    #endregion

    #region Fields
    private CancellationTokenSource? _clickCts;
    private string? _clickAccentPath;
    private string? _clickPath;
    private double _gridOriginMs;
    private CancellationTokenSource? _transportCts;
    #endregion

    #region Private methods
    ///<summary>
    ///Applies Clear or Duplicate to a pattern in the Patterns layer.
    ///</summary>
    private void ApplyPatternTool(int pattern)
    {
        LaunchpadSequence sequence = _project.Sequence;
        if (Tool == LaunchpadTool.Clear)
        {
            PushUndo();
            sequence.Patterns[pattern] = new LaunchpadPattern();
            Say($"Cleared pattern {pattern + 1}.");
            Changed();
            return;
        }

        if (_duplicatePattern is not { } source)
        {
            _duplicatePattern = pattern;
            Say($"Copying pattern {pattern + 1}: now tap the pattern to copy it onto.");
            RefreshAll();
            return;
        }

        _duplicatePattern = null;
        if (source != pattern)
        {
            PushUndo();
            sequence.Patterns[pattern] = sequence.Patterns[source].Clone();
            Say($"Copied pattern {source + 1} to pattern {pattern + 1}.");
        }

        Changed();
    }

    ///<summary>
    ///Applies Clear or Duplicate to a step of the selected track.
    ///</summary>
    private void ApplyStepTool(int index)
    {
        List<LaunchpadStep> steps = CurrentPattern.Tracks[SelectedTrack];
        if (Tool == LaunchpadTool.Clear)
        {
            PushUndo();
            steps[index] = new LaunchpadStep();
            Changed();
            return;
        }

        if (_duplicateStep is not { } source)
        {
            _duplicateStep = index;
            RefreshAll();
            Say($"Copying step {index + 1}: now tap the step to copy it onto.");
            return;
        }

        _duplicateStep = null;
        if (source != index)
        {
            PushUndo();
            steps[index] = steps[source].Clone();
            Say($"Copied step {source + 1} to step {index + 1}.");
        }

        Changed();
    }

    ///<summary>
    ///Turns the last two bars of pad hits into the selected pattern: the four most-played samples become the four tracks and
    ///each hit becomes a step.
    ///</summary>
    private void Capture()
    {
        double stepMs = 15000.0 / _project.Tempo;
        double now = _clock.Elapsed.TotalMilliseconds;
        List<CapturedHit> recent = [.. _hits.Where(hit => now - hit.TimeMs <= LaunchpadPattern.StepCount * stepMs)];
        if (recent.Count == 0)
        {
            Say("Nothing to capture: play some pads first, then press Capture. It keeps the last two bars.");
            return;
        }

        List<IGrouping<string, CapturedHit>> samples = [.. recent.GroupBy(hit => hit.ClipPath).OrderByDescending(group => group.Count()).Take(LaunchpadPattern.TrackCount)];
        PushUndo();

        LaunchpadPattern pattern = CurrentPattern;
        double start = recent.Min(hit => hit.TimeMs);
        int lastStep = 0;
        int written = 0;
        for (int track = 0; track < LaunchpadPattern.TrackCount; track++)
        {
            pattern.Tracks[track] = LaunchpadPattern.NewTrack();
        }

        for (int track = 0; track < samples.Count; track++)
        {
            CapturedHit first = samples[track].First();
            _project.Sequence.Tracks[track] = new LaunchpadTrack { ClipPath = first.ClipPath, Label = first.Label, ColorHex = first.ColorHex, Column = first.Column };
            foreach (CapturedHit hit in samples[track])
            {
                double position = (hit.TimeMs - start) / stepMs;
                int step = _isRecordQuantiseOn ? (int)Math.Round(position) : (int)Math.Floor(position);
                int micro = _isRecordQuantiseOn ? 0 : (int)Math.Round((position - step) * 4);
                if (micro == 4)
                {
                    step++;
                    micro = 0;
                }

                if (step >= LaunchpadPattern.StepCount)
                {
                    continue;
                }

                LaunchpadStep target = pattern.Tracks[track][step];
                target.IsOn = true;
                target.MicroStep = micro;
                lastStep = Math.Max(lastStep, step);
                written++;
            }
        }

        pattern.Length = Math.Clamp(((lastStep / 4) + 1) * 4, 4, LaunchpadPattern.StepCount);
        _hits.Clear();
        Mode = LaunchpadMode.Sequencer;
        Layer = LaunchpadLayer.None;
        SelectedTrack = 0;
        Say($"Captured {written} hit{(written == 1 ? string.Empty : "s")} into pattern {_project.Sequence.SelectedPattern + 1}, on {samples.Count} track{(samples.Count == 1 ? string.Empty : "s")}. Press Play to hear it.");
        Changed();
    }

    ///<summary>
    ///Empties the selected track's sample (its steps stay).
    ///</summary>
    private void ClearTrackSample()
    {
        PushUndo();
        _project.Sequence.Tracks[SelectedTrack] = new LaunchpadTrack();
        Say($"Track {SelectedTrack + 1} has no sample now.");
        Changed();
    }

    ///<summary>
    ///Doubles the pattern's length by repeating what it has, up to 32 steps.
    ///</summary>
    private void DoublePattern()
    {
        LaunchpadPattern pattern = CurrentPattern;
        int length = pattern.Length;
        int doubled = Math.Min(LaunchpadPattern.StepCount, length * 2);
        if (doubled == length)
        {
            Say("The pattern is already 32 steps, the longest it can be.");
            return;
        }

        PushUndo();
        foreach (List<LaunchpadStep> track in pattern.Tracks)
        {
            for (int i = length; i < doubled; i++)
            {
                track[i] = track[i % length].Clone();
            }
        }

        pattern.Length = doubled;
        Say($"Doubled: the pattern is {doubled} steps now.");
        Changed();
    }

    ///<summary>
    ///Changes one of the pattern's settings from a pad tap: the top row is the length, the second the direction, the third the
    ///speed.
    ///</summary>
    private void EditPatternSettings(LaunchpadPadViewModel pad)
    {
        LaunchpadPattern pattern = CurrentPattern;
        switch (pad.Row)
        {
            case 0:
                PushUndo();
                pattern.Length = (pad.Column + 1) * 4;
                Say($"Pattern length {pattern.Length} steps.");
                break;
            case 1 when pad.Column < Enum.GetValues<PatternDirection>().Length:
                PushUndo();
                pattern.Direction = (PatternDirection)pad.Column;
                Say($"Direction: {pattern.Direction}.");
                break;
            case 2 when pad.Column < PatternSpeeds.Length:
                PushUndo();
                pattern.Speed = PatternSpeeds[pad.Column];
                Say($"Pattern speed {pattern.Speed:0.#}x.");
                break;
            default:
                break;
        }
    }

    ///<summary>
    ///Tapping a pad in the sequencer: a step (in the top rows) is edited for the layer showing, and a sample slot (in the
    ///bottom rows) becomes the selected track's sample.
    ///</summary>
    private void EditSequencer(LaunchpadPadViewModel pad)
    {
        if (pad.Row >= StepRows)
        {
            SetTrackSample(pad);
            return;
        }

        int index = (pad.Row * Columns) + pad.Column;
        LaunchpadPattern pattern = CurrentPattern;
        if (index >= pattern.Length)
        {
            Say($"Step {index + 1} is past the end of the pattern ({pattern.Length} steps). Pattern Settings changes the length.");
            return;
        }

        LaunchpadTrack track = _project.Sequence.Tracks[SelectedTrack];
        LaunchpadStep step = pattern.Tracks[SelectedTrack][index];
        switch (StepLayer)
        {
            case LaunchpadLayer.Velocity when step.IsOn:
                PushUndo();
                step.Velocity = VelocityLevels[(Array.IndexOf(VelocityLevels, step.Velocity) + 1) % VelocityLevels.Length];
                Say($"Step {index + 1} velocity {step.Velocity * 100:0}%.");
                break;
            case LaunchpadLayer.Probability when step.IsOn:
                PushUndo();
                step.Probability = ProbabilityLevels[(Array.IndexOf(ProbabilityLevels, step.Probability) + 1) % ProbabilityLevels.Length];
                Say($"Step {index + 1} plays {step.Probability}% of the time.");
                break;
            case LaunchpadLayer.MicroStep when step.IsOn:
                PushUndo();
                step.MicroStep = (step.MicroStep + 1) % 4;
                Say($"Step {index + 1} nudged {step.MicroStep * 25}% of a step late.");
                break;
            case LaunchpadLayer.Steps:
                if (!track.HasSource)
                {
                    Say($"Track {SelectedTrack + 1} has no sample yet: tap a pad in the bottom four rows first.");
                    return;
                }

                PushUndo();
                step.IsOn = !step.IsOn;
                if (step.IsOn)
                {
                    PlayClip(TrackVoiceBase + SelectedTrack, track.ClipPath!, track.Column, step.Velocity, 1, loop: false);
                }

                break;
            default:
                Say("Turn the step on in Steps first.");
                return;
        }

        Changed();
    }

    ///<summary>
    ///Waits for the next click time and plays the metronome, until stopped.
    ///</summary>
    private async Task RunClickAsync(CancellationToken token)
    {
        double next = _clock.Elapsed.TotalMilliseconds;
        int beat = 0;
        try
        {
            while (!token.IsCancellationRequested)
            {
                double wait = next - _clock.Elapsed.TotalMilliseconds;
                if (wait > 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(wait), token);
                }

                string? path = beat % 4 == 0 ? _clickAccentPath : _clickPath;
                if (path is not null)
                {
                    _playbackService.TriggerVoice(ClickVoiceKey, path, new PadVoiceOptions(0.7));
                }

                next += 60000.0 / _project.Tempo;
                beat++;
            }
        }
        catch (OperationCanceledException)
        {
            // Stopped.
        }
    }

    ///<summary>
    ///Waits for each step's time, then plays it, until stopped. Both the tempo and the pattern are read fresh for every step, so
    ///changing either while it runs takes effect straight away.
    ///</summary>
    private async Task RunTransportAsync(CancellationToken token)
    {
        double next = _clock.Elapsed.TotalMilliseconds;
        _gridOriginMs = next;
        int tick = 0;
        int position = -1;
        int direction = 1;
        try
        {
            while (!token.IsCancellationRequested)
            {
                LaunchpadPattern pattern = CurrentPattern;
                double stepMs = 15000.0 / _project.Tempo / pattern.Speed;
                double swingMs = tick % 2 == 1 ? _project.SwingLevel * 0.07 * stepMs : 0;
                double wait = next + swingMs - _clock.Elapsed.TotalMilliseconds;
                if (wait > 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(wait), token);
                }
                else if (wait < -stepMs * 4)
                {
                    // The app was paused; carry on from now instead of playing all the missed steps at once.
                    next = _clock.Elapsed.TotalMilliseconds;
                }

                position = NextPosition(pattern, position, ref direction);
                FireStep(pattern, position, stepMs);
                PlayheadStep = position;
                next += stepMs;
                tick++;
            }
        }
        catch (OperationCanceledException)
        {
            // Stopped.
        }
    }

    ///<summary>
    ///Plays every track's step at <paramref name="position"/>: those that are on, that pass their probability roll, delayed by
    ///their micro step.
    ///</summary>
    private void FireStep(LaunchpadPattern pattern, int position, double stepMs)
    {
        for (int track = 0; track < LaunchpadPattern.TrackCount; track++)
        {
            LaunchpadTrack source = _project.Sequence.Tracks[track];
            LaunchpadStep step = pattern.Tracks[track][position];
            bool isSilent = !source.HasSource || !step.IsOn || (step.Probability < 100 && _random.Next(100) >= step.Probability);
            if (isSilent)
            {
                continue;
            }

            PlayClip(TrackVoiceBase + track, source.ClipPath!, source.Column, step.Velocity, 1, loop: false, delayMs: step.MicroStep * 0.25 * stepMs);
        }
    }

    ///<summary>
    ///Randomly changes the selected pattern: some steps flip, and some that are on get a new velocity. Only tracks that have a
    ///sample are touched, since the others couldn't be heard.
    ///</summary>
    private void Mutate()
    {
        PushUndo();
        LaunchpadPattern pattern = CurrentPattern;
        int changed = 0;
        for (int track = 0; track < LaunchpadPattern.TrackCount; track++)
        {
            if (!_project.Sequence.Tracks[track].HasSource)
            {
                continue;
            }

            for (int i = 0; i < pattern.Length; i++)
            {
                LaunchpadStep step = pattern.Tracks[track][i];
                if (_random.NextDouble() < 0.12)
                {
                    step.IsOn = !step.IsOn;
                    changed++;
                }
                else if (step.IsOn && _random.NextDouble() < 0.2)
                {
                    step.Velocity = VelocityLevels[_random.Next(VelocityLevels.Length)];
                    changed++;
                }
            }
        }

        _ = FlashKeyAsync(LaunchpadControl.Mutation);
        Say(changed == 0 ? "Mutation changed nothing this time; press it again." : $"Mutated pattern {_project.Sequence.SelectedPattern + 1}: {changed} change{(changed == 1 ? string.Empty : "s")}. Undo brings it back.");
        Changed();
    }

    ///<summary>
    ///Which step comes next, for the pattern's direction. <paramref name="direction"/> is +1 or -1 for ping-pong.
    ///</summary>
    private int NextPosition(LaunchpadPattern pattern, int position, ref int direction)
    {
        int length = pattern.Length;
        if (position < 0)
        {
            direction = 1;
            return pattern.Direction switch
            {
                PatternDirection.Backward => length - 1,
                PatternDirection.Random => _random.Next(length),
                _ => 0,
            };
        }

        int current = position % length;
        switch (pattern.Direction)
        {
            case PatternDirection.Backward:
                return (current - 1 + length) % length;
            case PatternDirection.PingPong:
                if (length == 1)
                {
                    return 0;
                }

                int next = current + direction;
                if (next >= length)
                {
                    direction = -1;
                    next = length - 2;
                }
                else if (next < 0)
                {
                    direction = 1;
                    next = 1;
                }

                return next;
            case PatternDirection.Random:
                return _random.Next(length);
            default:
                return (current + 1) % length;
        }
    }

    ///<summary>
    ///Milliseconds until the next sixteenth-note line of the tempo grid, or 0 if a hit is close enough after a line to count as
    ///on it. The grid starts when the sequencer was last started, so pads played along with it fall on its steps.
    ///</summary>
    private double MsToNextGridLine()
    {
        double stepMs = 15000.0 / _project.Tempo;
        double intoStep = (_clock.Elapsed.TotalMilliseconds - _gridOriginMs) % stepMs;
        return intoStep < stepMs * 0.12 ? 0 : stepMs - intoStep;
    }

    ///<summary>
    ///Renders the selected pattern once, as it would play now, to a new clip in the library.
    ///</summary>
    private async Task PrintToClipAsync()
    {
        _ = FlashKeyAsync(LaunchpadControl.PrintToClip);
        LaunchpadPattern pattern = CurrentPattern;
        double stepMs = 15000.0 / _project.Tempo / pattern.Speed;
        double swing = _project.SwingLevel * 0.07;
        MixProject mix = new() { Name = $"Launchpad pattern {_project.Sequence.SelectedPattern + 1}" };

        for (int track = 0; track < LaunchpadPattern.TrackCount; track++)
        {
            LaunchpadTrack source = _project.Sequence.Tracks[track];
            if (!source.HasSource || !IsColumnAudible(source.Column))
            {
                continue;
            }

            LaunchpadColumn column = _project.Columns[source.Column];
            Track printed = new() { Name = source.Label, Volume = column.Volume * _project.MasterVolume, Pan = Math.Clamp(column.Pan + _project.MasterPan, -1, 1) };
            for (int i = 0; i < pattern.Length; i++)
            {
                LaunchpadStep step = pattern.Tracks[track][i];
                bool isSilent = !step.IsOn || (step.Probability < 100 && _random.Next(100) >= step.Probability);
                if (isSilent)
                {
                    continue;
                }

                double startMs = (i * stepMs) + (i % 2 == 1 ? swing * stepMs : 0) + (step.MicroStep * 0.25 * stepMs);
                printed.Clips.Add(new TrackClip { ClipFilePath = source.ClipPath!, ClipName = source.Label, StartOffset = TimeSpan.FromMilliseconds(startMs), GainDb = 20 * Math.Log10(Math.Max(step.Velocity, 0.01)) });
            }

            if (printed.Clips.Count > 0)
            {
                mix.Tracks.Add(printed);
            }
        }

        if (mix.Tracks.Count == 0)
        {
            Say("Nothing to print: give a track a sample and turn some of its steps on first.");
            return;
        }

        IsBusy = true;
        try
        {
            string name = $"Launchpad pattern {_project.Sequence.SelectedPattern + 1} {_project.Tempo} BPM";
            string path = await _mixdownService.RenderAsync(mix, name);
            AudioClip clip = await _libraryService.AddClipAsync(path, name);
            Say($"Printed '{clip.Name}' to your library.");
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or InvalidDataException or NotSupportedException or UnauthorizedAccessException or OutOfMemoryException)
        {
            Log_PrintFailed(ex);
            Say("Couldn't print the pattern.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to print the pattern to a clip.")]
    private partial void Log_PrintFailed(Exception exception);

    ///<summary>
    ///Chooses which of the eight patterns is played and edited.
    ///</summary>
    private void SelectPattern(int pattern)
    {
        _project.Sequence.SelectedPattern = pattern;
        Say($"Pattern {pattern + 1}.");
    }

    ///<summary>
    ///Sets the tempo (40 to 240 beats per minute).
    ///</summary>
    private void SetTempo(int bpm) => _project.Tempo = Math.Clamp(bpm, 40, 240);

    ///<summary>
    ///Gives the selected track the sample on the tapped pad.
    ///</summary>
    private void SetTrackSample(LaunchpadPadViewModel pad)
    {
        if (!pad.HasClip)
        {
            Say("That pad is empty: choose one with a sample.");
            return;
        }

        PushUndo();
        _project.Sequence.Tracks[SelectedTrack] = new LaunchpadTrack { ClipPath = pad.Pad.ClipPath, Label = pad.Pad.Label, ColorHex = ColumnColors[pad.Column], Column = pad.Column };
        PlayClip(TrackVoiceBase + SelectedTrack, pad.Pad.ClipPath!, pad.Column, 1, 1, loop: false);
        Say($"Track {SelectedTrack + 1} plays '{pad.Pad.Label}'.");
        Changed();
    }

    ///<summary>
    ///Starts the metronome, first making its two click sounds (a high one for the first beat of a bar) if they don't exist yet.
    ///</summary>
    private async Task StartClickAsync()
    {
        try
        {
            if (_clickPath is null || _clickAccentPath is null)
            {
                AudioClip normal = await _synthesisService.GenerateToneAsync(WaveformType.Sine, 1500, 0.03, 0.8, "click");
                AudioClip accent = await _synthesisService.GenerateToneAsync(WaveformType.Sine, 2300, 0.03, 0.9, "click accent");
                _clickPath = normal.FilePath;
                _clickAccentPath = accent.FilePath;
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException or UnauthorizedAccessException)
        {
            Log_ClickFailed(ex);
            _isClickOn = false;
            Say("Couldn't make the click sound.");
            RefreshKeys();
            return;
        }

        StopClick();
        if (_isClickOn)
        {
            _clickCts = new CancellationTokenSource();
            _ = RunClickAsync(_clickCts.Token);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to make the metronome click.")]
    private partial void Log_ClickFailed(Exception exception);

    ///<summary>
    ///Stops the metronome.
    ///</summary>
    private void StopClick()
    {
        _clickCts?.Cancel();
        _clickCts?.Dispose();
        _clickCts = null;
    }

    ///<summary>
    ///Stops the sequencer, and silences its sounds.
    ///</summary>
    private void StopTransport()
    {
        _transportCts?.Cancel();
        _transportCts?.Dispose();
        _transportCts = null;
        IsPlaying = false;
        PlayheadStep = -1;
        for (int track = 0; track < LaunchpadPattern.TrackCount; track++)
        {
            _playbackService.StopPad(TrackVoiceBase + track);
        }
    }

    ///<summary>
    ///Starts the sequencer from the first step, or stops it.
    ///</summary>
    private void TogglePlay()
    {
        if (IsPlaying)
        {
            StopTransport();
            Say("Stopped.");
            return;
        }

        _transportCts = new CancellationTokenSource();
        IsPlaying = true;
        _ = RunTransportAsync(_transportCts.Token);
        if (_isClickOn)
        {
            _ = StartClickAsync();
        }

        Say($"Playing pattern {_project.Sequence.SelectedPattern + 1}.");
    }
    #endregion

    #region Public properties
    ///<summary>
    ///The pattern being edited and played.
    ///</summary>
    private LaunchpadPattern CurrentPattern => _project.Sequence.Patterns[_project.Sequence.SelectedPattern];
    #endregion
}
