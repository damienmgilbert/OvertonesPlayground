using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Tests.Services;

///<summary>
///Holds the tutorials to what they promise: every button is taught, every step points at something real, the tips are short
///enough to sit next to a pad, and the sounds a step asks for are in the Sound Bank. The steps' actions are run against a host
///that only writes down what it was asked to do.
///</summary>
public sealed class LaunchpadTutorialsTests
{
    #region Constants
    ///<summary>
    ///The most a tip's text can be and still fit on a phone next to what it points at (the tip is 340 wide, and a long one covers the pads).
    ///</summary>
    private const int MaxTipLength = 290;
    #endregion

    #region Private methods
    private static async Task<RecordingHost> RunAsync(LaunchpadTutorial tutorial)
    {
        RecordingHost host = new();
        foreach (LaunchpadTutorialStep step in tutorial.Steps)
        {
            if (step.Perform is not null)
            {
                await step.Perform(host);
            }
        }

        return host;
    }

    public static TheoryData<string> TutorialIds()
    {
        TheoryData<string> ids = [];
        foreach (LaunchpadTutorial tutorial in LaunchpadTutorials.All)
        {
            ids.Add(tutorial.Id);
        }

        return ids;
    }

    private static LaunchpadTutorial Tutorial(string id) => LaunchpadTutorials.All.Single(tutorial => tutorial.Id == id);
    #endregion

    #region Public methods
    [Fact]
    public void All_ListsTheNineTutorialsInTheOrderTheyAreTaught() =>
        Assert.Equal(["pads", "rhythm", "timing", "feel", "live", "melody", "harmony", "timbre", "mix"], LaunchpadTutorials.All.Select(tutorial => tutorial.Id));

    [Fact]
    public void All_NumbersTheTitlesInOrder() =>
        Assert.All(LaunchpadTutorials.All.Select((tutorial, index) => (tutorial, index)), pair => Assert.StartsWith($"{pair.index + 1}. ", pair.tutorial.Title, StringComparison.Ordinal));

    [Theory]
    [MemberData(nameof(TutorialIds))]
    public void EveryTutorial_StartsOnALessonSetupThatExists(string id)
    {
        LaunchpadTutorial tutorial = Tutorial(id);

        Assert.Contains(tutorial.LessonId, LaunchpadLessons.Ids);
        Assert.False(string.IsNullOrWhiteSpace(tutorial.Summary));
        Assert.True(tutorial.Steps.Count >= 4, "A tutorial needs enough steps to teach something.");
    }

    [Theory]
    [MemberData(nameof(TutorialIds))]
    public void EveryStep_HasATitleAndTipsShortEnoughToFit(string id)
    {
        foreach (LaunchpadTutorialStep step in Tutorial(id).Steps)
        {
            Assert.False(string.IsNullOrWhiteSpace(step.Title), "A step needs a title.");
            Assert.True(step.Title.Length <= 32, $"'{step.Title}' is too long for a tip's heading.");
            Assert.False(string.IsNullOrWhiteSpace(step.Message), $"'{step.Title}' needs a message.");
            Assert.True(step.Message.Length <= MaxTipLength, $"'{step.Title}' message is {step.Message.Length} characters; keep it to {MaxTipLength}.");
            if (step.Result is not null)
            {
                Assert.True(step.Perform is not null, $"'{step.Title}' has a result but nothing to perform.");
                Assert.True(step.Result.Length <= MaxTipLength, $"'{step.Title}' result is {step.Result.Length} characters; keep it to {MaxTipLength}.");
            }
        }
    }

    [Theory]
    [MemberData(nameof(TutorialIds))]
    public void EveryStep_PointsAtExactlyOneThingAndOutlinesOnlyRealOnes(string id)
    {
        foreach (LaunchpadTutorialStep step in Tutorial(id).Steps)
        {
            LaunchpadTutorialFocus focus = step.Focus;
            Assert.True(focus.PointerCount == 1, $"'{step.Title}' must point at exactly one thing.");
            Assert.All(focus.HighlightedPads, pad => Assert.InRange(pad, 0, LaunchpadTutorialFocus.PadCount - 1));
            Assert.All(focus.HighlightedTracks, column => Assert.InRange(column, 0, LaunchpadProject.ColumnCount - 1));
            Assert.All(focus.HighlightedKeys, key => Assert.NotEqual(LaunchpadControl.Track, key));
        }
    }

    [Fact]
    public async Task Together_TheyTeachEveryButton()
    {
        HashSet<LaunchpadControl> taught = [];
        foreach (LaunchpadTutorial tutorial in LaunchpadTutorials.All)
        {
            foreach (LaunchpadControl key in tutorial.Steps.SelectMany(step => step.Focus.HighlightedKeys))
            {
                _ = taught.Add(key);
            }

            RecordingHost host = await RunAsync(tutorial);
            foreach (LaunchpadControl pressed in host.Pressed)
            {
                _ = taught.Add(pressed);
            }

            if (tutorial.Steps.Any(step => step.Focus.HighlightedTracks.Any()) || host.TrackPresses.Count > 0)
            {
                _ = taught.Add(LaunchpadControl.Track);
            }
        }

        Assert.DoesNotContain(Enum.GetValues<LaunchpadControl>(), control => !taught.Contains(control));
    }

    [Fact]
    public async Task Together_TheyUseEveryShiftedFunction()
    {
        HashSet<LaunchpadControl> shifted = [];
        foreach (LaunchpadTutorial tutorial in LaunchpadTutorials.All)
        {
            RecordingHost host = await RunAsync(tutorial);
            foreach (LaunchpadControl control in host.ShiftedPresses)
            {
                _ = shifted.Add(control);
            }
        }

        // Every button with a second function: Projects (Save) is a menu, so it is described but not run.
        LaunchpadControl[] expected = [LaunchpadControl.Duplicate, LaunchpadControl.Quantise, LaunchpadControl.RecordArm, LaunchpadControl.Mute, LaunchpadControl.Solo, LaunchpadControl.Volume, LaunchpadControl.Pan, LaunchpadControl.Sends, LaunchpadControl.Device, LaunchpadControl.StopClip];
        Assert.DoesNotContain(expected, control => !shifted.Contains(control));
    }

    [Theory]
    [MemberData(nameof(TutorialIds))]
    public async Task EveryAction_TapsPadsThatExistAndPressesRealButtons(string id)
    {
        RecordingHost host = await RunAsync(Tutorial(id));

        Assert.All(host.Tapped, pad => Assert.InRange(pad, -1, LaunchpadTutorialFocus.PadCount - 1));
        Assert.All(host.Held, pad => Assert.InRange(pad, 0, LaunchpadTutorialFocus.PadCount - 1));
        Assert.All(host.TrackPresses, column => Assert.InRange(column, 0, LaunchpadProject.ColumnCount - 1));
        Assert.DoesNotContain(LaunchpadControl.Track, host.Pressed);
        Assert.All(host.Banks, bank => Assert.InRange(bank, 0, LaunchpadProject.BankCount - 1));
        Assert.All(host.Scales, scale => Assert.InRange(scale, 0, LaunchpadScale.Count - 1));
    }

    [Fact]
    public async Task EverySoundAStepPutsOnAPad_IsInTheSoundBank()
    {
        List<string> names = [];
        foreach (LaunchpadTutorial tutorial in LaunchpadTutorials.All)
        {
            names.AddRange((await RunAsync(tutorial)).Assigned.Select(assignment => assignment.SampleName));
        }

        Assert.NotEmpty(names);
        Assert.All(names, name => Assert.NotNull(RealCatalog.Index.Find(name + ".wav")));
    }

    [Fact]
    public async Task ThePadsATutorialAssignsAreEmptyInItsSetup()
    {
        foreach (LaunchpadTutorial tutorial in LaunchpadTutorials.All)
        {
            RecordingHost host = await RunAsync(tutorial);
            LaunchpadProject project = LaunchpadLessons.Build(tutorial.LessonId, RealCatalog.Index, sample => sample.Id);
            foreach ((int pad, _) in host.Assigned)
            {
                Assert.DoesNotContain(project.Pads, existing => existing.Bank == 0 && existing.Index == pad);
                Assert.DoesNotContain(project.Pads, existing => existing.Bank == 1 && existing.Index == pad);
            }
        }
    }

    [Theory]
    [MemberData(nameof(TutorialIds))]
    public void EveryTutorial_NeverEndsRightAfterAnActionWithNothingToRead(string id)
    {
        // Playing a step and then finishing at once would take the tip away with nothing said about what just happened.
        LaunchpadTutorialStep last = Tutorial(id).Steps[^1];

        Assert.True(last.Perform is null || last.Result is not null, $"The last step of '{id}' plays something, so it needs a result to read afterwards.");
    }

    [Theory]
    [MemberData(nameof(TutorialIds))]
    public void EveryTutorial_EndsByLeadingOnToTheNextOrSayingItIsTheLast(string id)
    {
        LaunchpadTutorialStep last = Tutorial(id).Steps[^1];
        string text = last.Result ?? last.Message;

        Assert.True(text.Contains("Next", StringComparison.Ordinal) || text.Contains("tour", StringComparison.OrdinalIgnoreCase) || text.Contains("Go make", StringComparison.Ordinal), $"The last step of '{id}' should say where to go from here.");
    }
    #endregion

    #region Nested types
    ///<summary>
    ///A host that only records what it is asked to do, and does it instantly.
    ///</summary>
    private sealed class RecordingHost : ILaunchpadTutorialHost
    {
        public List<(int Pad, string SampleName)> Assigned { get; } = [];

        public List<int> Banks { get; } = [];

        public List<int> Held { get; } = [];

        public List<LaunchpadControl> Pressed { get; } = [];

        public List<int> Scales { get; } = [];

        public List<LaunchpadControl> ShiftedPresses { get; } = [];

        public List<int> Tapped { get; } = [];

        public List<int> TrackPresses { get; } = [];

        public Task AssignFromSoundBankAsync(int pad, string sampleName)
        {
            Assigned.Add((pad, sampleName));
            return Task.CompletedTask;
        }

        public Task HoldAsync(int pad, int milliseconds)
        {
            Held.Add(pad);
            return Task.CompletedTask;
        }

        public Task LoadLessonAsync(string lessonId) => Task.CompletedTask;

        public Task PressAsync(LaunchpadControl control, bool shifted = false)
        {
            Pressed.Add(control);
            if (shifted)
            {
                ShiftedPresses.Add(control);
            }

            return Task.CompletedTask;
        }

        public Task PressTrackAsync(int column)
        {
            TrackPresses.Add(column);
            return Task.CompletedTask;
        }

        public Task ResetMixerAsync() => Task.CompletedTask;

        public Task SetEditModeAsync(bool isOn) => Task.CompletedTask;

        public Task SetScaleAsync(int scaleIndex)
        {
            Scales.Add(scaleIndex);
            return Task.CompletedTask;
        }

        public Task ShowBankAsync(int bank)
        {
            Banks.Add(bank);
            return Task.CompletedTask;
        }

        public Task StopAllAsync() => Task.CompletedTask;

        public Task TapAsync(IReadOnlyList<int> pads, int gapMilliseconds)
        {
            Tapped.AddRange(pads);
            return Task.CompletedTask;
        }

        public Task ToggleLoopAsync(int pad) => Task.CompletedTask;

        public Task WaitAsync(int milliseconds) => Task.CompletedTask;
    }
    #endregion
}
