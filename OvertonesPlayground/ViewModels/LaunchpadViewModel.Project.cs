using System.Text.Json;
using OvertonesPlayground.Models;

namespace OvertonesPlayground.ViewModels;

///<summary>
///What the Launchpad remembers: saving after every change, restoring at startup, undo, named projects, and the Setup menu.
///</summary>
public partial class LaunchpadViewModel
{
    #region Constants
    private const string LayoutFileName = "launchpad.json";
    private const string ProjectsFolderName = "LaunchpadProjects";
    private const int UndoLimit = 30;
    #endregion

    #region Fields
    private readonly List<string> _undo = [];
    private static string LayoutFilePath => Path.Combine(FileSystem.AppDataDirectory, LayoutFileName);
    #endregion

    #region Private methods
    ///<summary>
    ///Makes a loaded project the current one: fills the banks, checks that every sample file is still there, and shows the bank
    ///it was saved on.
    ///</summary>
    private void ApplyProject(LaunchpadProject project)
    {
        project.Normalize();
        _project = project;

        foreach (LaunchpadPad pad in _banks.SelectMany(bank => bank))
        {
            pad.ClipPath = null;
            pad.Label = string.Empty;
            pad.IsLooping = false;
            pad.Volume = 1;
        }

        int restored = 0;
        foreach (LaunchpadPad saved in project.Pads)
        {
            bool isUnusable = saved.Bank is < 0 or >= LaunchpadProject.BankCount || saved.Index is < 0 or >= LaunchpadProject.PadsPerBank || !saved.HasClip || !File.Exists(saved.ClipPath);
            if (isUnusable)
            {
                continue;
            }

            LaunchpadPad target = _banks[saved.Bank][saved.Index];
            target.ClipPath = saved.ClipPath;
            target.Label = saved.Label;
            target.ColorHex = ColumnColors[saved.Index % Columns];
            target.IsLooping = saved.IsLooping;
            target.Volume = saved.Volume;
            restored++;
        }

        // A track whose sample has since been deleted plays nothing rather than failing on every step.
        for (int track = 0; track < project.Sequence.Tracks.Count; track++)
        {
            LaunchpadTrack source = project.Sequence.Tracks[track];
            if (source.HasSource && !File.Exists(source.ClipPath))
            {
                project.Sequence.Tracks[track] = new LaunchpadTrack();
            }
        }

        Bank = project.Bank;
        for (int i = 0; i < Pads.Count; i++)
        {
            Pads[i].Bind(_banks[Bank][i]);
        }

        Log_LayoutRestored(restored);
    }

    ///<summary>
    ///Puts the current project's pads together with everything else, ready to save.
    ///</summary>
    private LaunchpadProject BuildProject()
    {
        _project.Bank = Bank;
        _project.Pads = [.. _banks.SelectMany(bank => bank).Where(pad => pad.HasClip)];
        return _project;
    }

    ///<summary>
    ///Called after every change: redraws, and saves, so the layout is already on disk whenever Android stops or reclaims the app.
    ///</summary>
    private void Changed()
    {
        RefreshAll();
        SaveLayout();
    }

    ///<summary>
    ///Goes to the next scale for Note and Chord modes.
    ///</summary>
    private void CycleScale()
    {
        _project.ScaleIndex = (_project.ScaleIndex + 1) % LaunchpadScale.Count;
        Say($"Scale: {LaunchpadScale.Name(_project.ScaleIndex)}.");
        Changed();
    }

    ///<summary>
    ///Asks the page for a project name, offering <paramref name="suggestion"/>.
    ///</summary>
    private async Task<string?> AskProjectNameAsync(string suggestion)
    {
        if (TextPrompt is null)
        {
            return string.IsNullOrWhiteSpace(suggestion) ? $"Project {DateTime.Now:yyyy-MM-dd HH-mm}" : suggestion;
        }

        string? name = await TextPrompt("Save project", "Name this Launchpad project:", suggestion);
        return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    }

    ///<summary>
    ///Reads a saved project. The first version of the Launchpad saved only a list of pads, which becomes bank A.
    ///</summary>
    private static LaunchpadProject ParseProject(string json)
    {
        bool isLegacy = json.TrimStart().StartsWith('[');
        return isLegacy
            ? new LaunchpadProject { Pads = JsonSerializer.Deserialize<List<LaunchpadPad>>(json) ?? [] }
            : JsonSerializer.Deserialize<LaunchpadProject>(json) ?? new LaunchpadProject();
    }

    ///<summary>
    ///Where the named project is kept.
    ///</summary>
    private static string ProjectPath(string name)
    {
        string directory = Path.Combine(FileSystem.AppDataDirectory, ProjectsFolderName);
        _ = Directory.CreateDirectory(directory);
        string safe = string.Concat(name.Trim().Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return Path.Combine(directory, $"{safe}.json");
    }

    ///<summary>
    ///The names of the saved projects.
    ///</summary>
    private static List<string> ProjectNames()
    {
        string directory = Path.GetDirectoryName(ProjectPath("x"))!;
        return [.. Directory.EnumerateFiles(directory, "*.json").Select(path => Path.GetFileNameWithoutExtension(path)).Order(StringComparer.OrdinalIgnoreCase)];
    }

    ///<summary>
    ///Remembers the state before an edit, so Undo can bring it back.
    ///</summary>
    private void PushUndo()
    {
        _undo.Add(JsonSerializer.Serialize(BuildProject()));
        if (_undo.Count > UndoLimit)
        {
            _undo.RemoveAt(0);
        }
    }

    ///<summary>
    ///Puts back the state before the last edit. Returns false if there is nothing to undo.
    ///</summary>
    private bool TryUndo()
    {
        if (_undo.Count == 0)
        {
            return false;
        }

        string json = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        LaunchpadProject previous = ParseProject(json);
        previous.Bank = Bank;
        ClearTool();
        ApplyProject(previous);
        Changed();
        return true;
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

            ApplyProject(ParseProject(File.ReadAllText(LayoutFilePath)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Log_LayoutRestoreFailed(ex);
        }
    }

    ///<summary>
    ///Writes the layout to app storage.
    ///</summary>
    private void SaveLayout()
    {
        try
        {
            File.WriteAllText(LayoutFilePath, JsonSerializer.Serialize(BuildProject()));
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

    ///<summary>
    ///Asks the page to show the Projects menu.
    ///</summary>
    private void OpenProjectsMenu()
    {
        List<LaunchpadMenuChoice> choices = [];
        if (!string.IsNullOrWhiteSpace(_project.Name))
        {
            choices.Add(new LaunchpadMenuChoice($"Save '{_project.Name}'", () => SaveProjectAsync(askForName: false)));
        }

        choices.Add(new LaunchpadMenuChoice("Save as...", () => SaveProjectAsync(askForName: true)));
        choices.Add(new LaunchpadMenuChoice("Open...", () =>
        {
            ShowProjectList("Open which project?", OpenProject);
            return Task.CompletedTask;
        }));
        choices.Add(new LaunchpadMenuChoice("New project", () =>
        {
            StartNewProject();
            return Task.CompletedTask;
        }));
        choices.Add(new LaunchpadMenuChoice("Delete...", () =>
        {
            ShowProjectList("Delete which project?", DeleteProject);
            return Task.CompletedTask;
        }));

        string title = string.IsNullOrWhiteSpace(_project.Name) ? "Projects (this one isn't saved yet)" : $"Projects: {_project.Name}";
        MenuRequested?.Invoke(this, new LaunchpadMenuEventArgs(title, choices));
    }

    ///<summary>
    ///Asks the page to list the saved projects, and runs <paramref name="then"/> on the one picked.
    ///</summary>
    private void ShowProjectList(string title, Action<string> then)
    {
        List<string> names = ProjectNames();
        if (names.Count == 0)
        {
            Say("No saved projects yet: use Save as...");
            return;
        }

        List<LaunchpadMenuChoice> choices = [.. names.Select(name => new LaunchpadMenuChoice(name, () =>
        {
            then(name);
            return Task.CompletedTask;
        }))];
        MenuRequested?.Invoke(this, new LaunchpadMenuEventArgs(title, choices));
    }

    ///<summary>
    ///Opens a saved project, replacing the current pads and sequencer.
    ///</summary>
    private void OpenProject(string name)
    {
        try
        {
            StopAll();
            PushUndo();
            LaunchpadProject project = ParseProject(File.ReadAllText(ProjectPath(name)));
            project.Name = name;
            ClearTool();
            ApplyProject(project);
            Say($"Opened '{name}'. Undo brings back what was here before.");
            Changed();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Log_LayoutRestoreFailed(ex);
            Say($"Couldn't open '{name}'.");
        }
    }

    ///<summary>
    ///Deletes a saved project's file. The current layout isn't touched.
    ///</summary>
    private void DeleteProject(string name)
    {
        try
        {
            File.Delete(ProjectPath(name));
            if (string.Equals(_project.Name, name, StringComparison.Ordinal))
            {
                _project.Name = string.Empty;
            }

            Say($"Deleted project '{name}'.");
            RefreshTexts();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Say($"Couldn't delete '{name}'.");
        }
    }

    ///<summary>
    ///Starts an empty project. What was there can still be brought back with Undo.
    ///</summary>
    private void StartNewProject()
    {
        StopAll();
        PushUndo();
        ClearTool();
        ApplyProject(new LaunchpadProject());
        Say("New project. Undo brings back what was here before.");
        Changed();
    }

    ///<summary>
    ///Saves the current layout as a named project, asking for the name first if it hasn't got one (or if
    ///<paramref name="askForName"/>).
    ///</summary>
    private async Task SaveProjectAsync(bool askForName)
    {
        string name = _project.Name;
        if (askForName || string.IsNullOrWhiteSpace(name))
        {
            string? chosen = await AskProjectNameAsync(name);
            if (chosen is null)
            {
                return;
            }

            name = chosen;
        }

        try
        {
            _project.Name = name;
            File.WriteAllText(ProjectPath(name), JsonSerializer.Serialize(BuildProject()));
            Say($"Saved project '{name}'.");
            _ = FlashKeyAsync(LaunchpadControl.Projects);
            SaveLayout();
            RefreshTexts();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log_LayoutSaveFailed(ex);
            Say($"Couldn't save '{name}'.");
        }
    }

    ///<summary>
    ///Asks the page to show the Setup menu.
    ///</summary>
    private void OpenSetupMenu()
    {
        List<LaunchpadMenuChoice> choices =
        [
            new LaunchpadMenuChoice($"Scale for Note and Chord: {LaunchpadScale.Name(_project.ScaleIndex)} (tap for the next)", () =>
            {
                CycleScale();
                return Task.CompletedTask;
            }),
            new LaunchpadMenuChoice("Reset the mixer, master and transpose", () =>
            {
                PushUndo();
                _project.Columns = [.. Enumerable.Range(0, LaunchpadProject.ColumnCount).Select(_ => new LaunchpadColumn())];
                _project.MasterPan = 0;
                _project.MasterVolume = 1;
                _project.Transpose = 0;
                Say("Mixer, master and transpose reset.");
                Changed();
                return Task.CompletedTask;
            }),
            new LaunchpadMenuChoice($"Clear bank {(char)('A' + Bank)}", () =>
            {
                PushUndo();
                foreach (LaunchpadPadViewModel pad in Pads)
                {
                    StopPad(pad);
                    pad.Clear();
                }

                Say($"Bank {(char)('A' + Bank)} cleared. Undo brings it back.");
                Changed();
                return Task.CompletedTask;
            }),
            new LaunchpadMenuChoice("Clear the sequencer", () =>
            {
                PushUndo();
                StopTransport();
                _project.Sequence = new LaunchpadSequence();
                Say("Sequencer cleared. Undo brings it back.");
                Changed();
                return Task.CompletedTask;
            }),
        ];

        MenuRequested?.Invoke(this, new LaunchpadMenuEventArgs("Setup", choices));
    }
    #endregion
}
