using OvertonesPlayground.Models;

namespace OvertonesPlayground.ViewModels;

///<summary>
///Ideas from the Sound Bank: ready-made setups in every style, newly generated projects, and sounds suggested for a pad, a column
///or a whole kit, each chosen by music theory and what the ontology knows about the sounds.
///</summary>
public partial class LaunchpadViewModel
{
    #region Constants
    private const int SuggestionCount = 8;
    #endregion

    #region Private methods
    ///<summary>
    ///Asks the page to list the styles that have ready-made setups, then the setups of the one picked.
    ///</summary>
    private void ShowPresetStyles()
    {
        List<LaunchpadMenuChoice> choices = [.. _examples.Styles
            .Where(style => _examples.Presets.Any(preset => preset.StyleKey == style.Key))
            .Select(style => new LaunchpadMenuChoice($"{style.Name} ({style.MinTempo}-{style.MaxTempo} BPM)", () =>
            {
                ShowPresets(style);
                return Task.CompletedTask;
            }))];
        MenuRequested?.Invoke(this, new LaunchpadMenuEventArgs("Ready-made setups: which style?", choices));
    }

    ///<summary>
    ///Asks the page to list a style's ready-made setups, and loads the one picked.
    ///</summary>
    private void ShowPresets(LaunchpadStyleInfo style)
    {
        List<LaunchpadMenuChoice> choices = [.. _examples.Presets.Where(preset => preset.StyleKey == style.Key).Select(preset => new LaunchpadMenuChoice(preset.Name, () => LoadExampleAsync(preset)))];
        MenuRequested?.Invoke(this, new LaunchpadMenuEventArgs($"{style.Name}: {style.Summary}", choices));
    }

    ///<summary>
    ///Asks the page to list the styles the generator knows, and generates a project in the one picked.
    ///</summary>
    private void ShowGenerateStyles()
    {
        List<LaunchpadMenuChoice> choices = [new LaunchpadMenuChoice("Surprise me (any style)", () => GenerateAsync(null))];
        choices.AddRange(_examples.Styles.Select(style => new LaunchpadMenuChoice($"{style.Name} · {style.Summary}", () => GenerateAsync(style.Key))));
        MenuRequested?.Invoke(this, new LaunchpadMenuEventArgs("Generate a new project in which style?", choices));
    }

    ///<summary>
    ///Generates a new project in style <paramref name="styleKey"/> (a random one if null) with a fresh seed and makes it the
    ///current project. What was here before can be brought back with Undo.
    ///</summary>
    internal async Task GenerateAsync(string? styleKey)
    {
        IReadOnlyList<LaunchpadStyleInfo> styles = _examples.Styles;
        if (styles.Count == 0)
        {
            return;
        }

        LaunchpadStyleInfo style = styles.FirstOrDefault(s => s.Key == styleKey) ?? styles[_random.Next(styles.Count)];
        IsBusy = true;
        Say($"Generating a {style.Name} project from the Sound Bank...");
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            LaunchpadGeneratedProject generated = await _examples.GenerateAsync(new LaunchpadGenerationRequest(style.Key, _random.Next()));
            StopAll();
            PushUndo();
            ClearTool();
            ApplyProject(generated.Project);
            Mode = LaunchpadMode.Session;
            Layer = LaunchpadLayer.None;
            Say($"Generated '{generated.Title}'. {generated.Description} Undo brings back what was here before.");
            Changed();
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                Log_Generated(generated.Title, (long)System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or UnauthorizedAccessException or ArgumentException or KeyNotFoundException or NotSupportedException)
        {
            Log_GenerateFailed(style.Key, ex);
            Say($"Couldn't generate a {style.Name} project.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Generated the Launchpad project '{Title}' in {Milliseconds} ms.")]
    private partial void Log_Generated(string title, long milliseconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Couldn't generate a Launchpad project in style {StyleKey}.")]
    private partial void Log_GenerateFailed(string styleKey, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Couldn't suggest sounds for the Launchpad.")]
    private partial void Log_SuggestFailed(Exception exception);

    ///<summary>
    ///Puts a Sound Bank sound on a pad of the bank on screen, copying it out of the app package first if needed.
    ///</summary>
    private async Task PlaceAsync(LaunchpadPadViewModel pad, string sampleName)
    {
        string path = await _examples.PrepareSampleAsync(sampleName);
        pad.Assign(path, sampleName, ColumnColors[pad.Column]);
    }

    ///<summary>
    ///Goes to the next key root for suggestions and generated sounds: none, C, C#, ... B, then none again. The key's scale is the
    ///one Note and Chord modes use.
    ///</summary>
    private void CycleKey()
    {
        _project.RootPitchClass = _project.RootPitchClass switch
        {
            null => 0,
            11 => null,
            { } root => root + 1,
        };
        Say(LaunchpadScale.KeyOf(_project) is { } key ? $"Key: {key.Name}. Suggestions keep to it." : "Key: none. Suggestions follow the sounds on the pads.");
        Changed();
    }

    ///<summary>
    ///Asks the page to list the kits, and swaps the drums of the bank on screen for the nearest sounds of the one picked.
    ///</summary>
    private async Task ShowKitSwapAsync()
    {
        try
        {
            IReadOnlyList<LaunchpadKitInfo> kits = await _examples.GetKitsAsync();
            List<LaunchpadMenuChoice> choices = [.. kits.Select(kit => new LaunchpadMenuChoice($"{kit.Name} ({kit.SampleCount} sounds)", () => SwapKitAsync(kit)))];
            MenuRequested?.Invoke(this, new LaunchpadMenuEventArgs($"Swap bank {(char)('A' + Bank)}'s drums for which kit?", choices));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Log_SuggestFailed(ex);
            Say("Couldn't read the Sound Bank's kits.");
        }
    }

    ///<summary>
    ///Swaps every drum of the bank on screen for the nearest sound of the same instrument from <paramref name="kit"/>. Pads that
    ///are not drums, and drums the kit has no match for, stay as they are. Undo brings the old drums back.
    ///</summary>
    private async Task SwapKitAsync(LaunchpadKitInfo kit)
    {
        IsBusy = true;
        try
        {
            IReadOnlyList<LaunchpadPadAssignment> swaps = await _examples.SuggestKitSwapAsync(BuildProject(), Bank, kit.Key);
            if (swaps.Count == 0)
            {
                Say($"Bank {(char)('A' + Bank)} has no drums {kit.Name} has matches for.");
                return;
            }

            PushUndo();
            foreach (LaunchpadPadAssignment swap in swaps)
            {
                await PlaceAsync(Pads[swap.PadIndex], swap.SampleName);
            }

            Say($"Swapped {swaps.Count} drum{(swaps.Count == 1 ? string.Empty : "s")} on bank {(char)('A' + Bank)} for {kit.Name} sounds. Undo brings the old ones back.");
            Changed();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            Log_SuggestFailed(ex);
            Say($"Couldn't swap to {kit.Name}.");
        }
        finally
        {
            IsBusy = false;
        }
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Starts loading the sound bank and preparing the generator in the background, so generating, loading a setup or suggesting
    ///a sound does not wait for them. Called when the Launchpad appears; a failure is only logged, and the work is retried when
    ///it is next needed.
    ///</summary>
    public void WarmUpIdeas() => _ = WarmUpIdeasAsync();

    private async Task WarmUpIdeasAsync()
    {
        try
        {
            await _examples.WarmUpAsync();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Log_SuggestFailed(ex);
        }
    }

    ///<summary>
    ///Asks the page to list sounds of the Sound Bank that suit <paramref name="pad"/>, each with why, and puts the one picked on
    ///it. For a pad with a sound they are alternatives like it; for an empty pad, sounds for what its column holds, in the
    ///project's key and at its tempo.
    ///</summary>
    public async Task SuggestForPadAsync(LaunchpadPadViewModel pad)
    {
        ArgumentNullException.ThrowIfNull(pad);
        IsBusy = true;
        try
        {
            IReadOnlyList<LaunchpadSuggestion> suggestions = await _examples.SuggestAsync(BuildProject(), Bank, pad.Index, SuggestionCount);
            if (suggestions.Count == 0)
            {
                Say("The Sound Bank has nothing that suits this pad in the project's key and tempo.");
                return;
            }

            List<LaunchpadMenuChoice> choices = [.. suggestions.Select(suggestion => new LaunchpadMenuChoice(suggestion.Label, async () =>
            {
                PushUndo();
                await PlaceAsync(pad, suggestion.SampleName);
                Say($"Pad {pad.Index + 1} now plays '{suggestion.SampleName}'. Undo brings back what was there.");
                Changed();
            }))];
            string title = pad.HasClip ? $"Sounds like '{pad.Label}'" : $"Sounds for pad {pad.Index + 1}";
            MenuRequested?.Invoke(this, new LaunchpadMenuEventArgs(title, choices));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            Log_SuggestFailed(ex);
            Say("Couldn't suggest sounds.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    ///<summary>
    ///Fills the empty pads of <paramref name="pad"/>'s column with suggested sounds, chosen together so they differ from each
    ///other. Undo empties them again.
    ///</summary>
    public async Task FillColumnAsync(LaunchpadPadViewModel pad)
    {
        ArgumentNullException.ThrowIfNull(pad);
        IsBusy = true;
        try
        {
            IReadOnlyList<LaunchpadPadAssignment> fills = await _examples.SuggestColumnAsync(BuildProject(), Bank, pad.Column);
            if (fills.Count == 0)
            {
                Say("This column has no empty pads, or nothing in the Sound Bank suits it.");
                return;
            }

            PushUndo();
            foreach (LaunchpadPadAssignment fill in fills)
            {
                await PlaceAsync(Pads[fill.PadIndex], fill.SampleName);
            }

            Say($"Filled {fills.Count} pad{(fills.Count == 1 ? string.Empty : "s")} of column {pad.Column + 1} from the Sound Bank. Undo empties them again.");
            Changed();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            Log_SuggestFailed(ex);
            Say("Couldn't fill the column.");
        }
        finally
        {
            IsBusy = false;
        }
    }
    #endregion
}
