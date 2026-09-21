using OvertonesPlayground.Controls;
using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;
using OvertonesPlayground.ViewModels;
#if ANDROID
using Microsoft.Maui.Platform;
#endif

namespace OvertonesPlayground.Views;

///<summary>
///Code-behind for the Launchpad page. Behavior lives in <see cref="LaunchpadViewModel"/>; this hosts what needs native UI (the
///pad menu, the Setup and Projects menus, the project-name prompt and the guide), and scales the device's text to the screen.
///</summary>
public partial class LaunchpadPage : ContentPage
{
    #region Constants
    ///<summary>
    ///The number of columns of cells in the device (the width of the middle eight pad columns plus a column at each side).
    ///</summary>
    private const double DeviceColumns = 10;

    ///<summary>
    ///The height of the device's rows added up in units of one pad row, matching the row definitions of the device's grid.
    ///</summary>
    private const double DeviceRowUnits = 10.6;

    // The AutomationIds of the top bar's buttons (see the page's ToolbarItems).
    private const string ToggleInfoButtonId = "launchpad-toggle-info";
    private const string EditButtonId = "launchpad-edit";
    private const string StopAllButtonId = "launchpad-stop-all";

    private const string ButtonGuideChoice = "Guide to every button";
    private const string AssignSampleChoice = "Assign sample from Sound Bank...";
    private const string ImportSampleChoice = "Import from this device...";
    private const string SuggestSampleChoice = "Suggest a sound from the Sound Bank...";
    private const string SimilarSampleChoice = "Swap for a similar sound...";
    private const string FillColumnChoice = "Fill this column's empty pads";
    private const string ClearPadChoice = "Clear pad";
    private const string LoopOffChoice = "Loop: turn off";
    private const string LoopOnChoice = "Loop: turn on";
    private const string StopPadChoice = "Stop this pad";

    private const string Guide = """
        TUTORIALS
        Tap the logo and choose a tutorial. Each one points at the buttons and pads it is about, then plays them for you with sounds from the Sound Bank: pads and loops, rhythm, timing, feel, playing live, melody, harmony, timbre and mixing. Your own pads come back when it ends.

        TOP ROW
        Shift: press it, then a button, to use the small label under that button's name.
        Left / Right arrows: previous / next pad bank (A to D).
        Session: each pad launches its sample.
        Note: the pads play the last sample you played as a scale, over two octaves.
        Chord: the pads play chords built on that sample.
        Custom: each pad sounds only while you hold it.
        Sequencer: a 4-track, 32-step sequencer. The top four rows are steps for the chosen track; tap a pad below to give the track a sample.
        Projects: save, open or start a project, load a ready-made setup in one of fifteen styles, or generate a new project from the Sound Bank. Shift + Projects saves the current one.

        LEFT COLUMN
        Up / Down arrows: transpose everything by a semitone.
        Clear: then tap a pad, step or pattern to clear it.
        Duplicate: tap something, then where to copy it. Shift + Duplicate (Double) repeats the pattern to twice its length.
        Quantise: pads you play wait for the next sixteenth note. Shift + Quantise (Record Quantise) snaps Capture to whole steps.
        Fixed Length: cuts every sound off after 1 beat, 2, 1 bar, 2 bars, or off.
        Play: starts and stops the sequencer.
        Capture: turns the last two bars you played into a pattern.

        RIGHT COLUMN (sequencer)
        Patterns: choose one of 8 patterns. Steps: turn steps on and off. Pattern Settings: length, direction, speed.
        Velocity, Probability, Micro Step: tap a step to cycle its loudness, chance of sounding, or how late it falls.
        Mutation: randomly changes the pattern. Print to Clip: renders the pattern to a new clip in your library.

        BUTTONS UNDER THE PADS
        The short row acts on a pad column (or, in the sequencer, chooses the track): with no function chosen it launches the column.
        Record Arm: arm columns for Capture. Shift: Undo.
        Mute / Solo: silence columns, or hear only some. Shift: Radio (one pad per column at a time) / Click (metronome).
        Volume / Pan / Sends / Device: the pads become a fader per column for level, left-right, echo, and speed. Shift: master volume, master pan, Tap tempo, Tempo.
        Stop Clip: stop a column. Shift: Swing.

        SETUP
        Scale for Note and Chord, the key suggestions keep to, swap a bank's drums for another kit, reset the mixer, clear a bank or the sequencer.

        SUGGESTIONS
        Long-press a pad (or use the pencil) and choose Suggest a sound: the Sound Bank sounds that suit that column, in the project's key and at its tempo, each with the reason. On a pad with a sound it offers similar ones; Fill this column fills its empty pads.

        TOP BAR
        Eye: hides or shows the hints and messages floating over the pads. Pencil: edit mode. Square: stops every sound.
        """;
    #endregion

    #region Fields
    private readonly ILogger<LaunchpadPage> _logger;
    private readonly ISamplePickerService _samplePicker;
    private readonly LaunchpadViewModel _viewModel;
    private bool _isMenuOpen;
    private bool _isClosingTutorialTip;
    private bool _tutorialActionPressed;
    private int _tutorialPromptVersion;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the page, binds it to its view model, and listens for menu requests.
    ///</summary>
    public LaunchpadPage(LaunchpadViewModel viewModel, ISamplePickerService samplePicker, ILogger<LaunchpadPage> logger)
    {
        _samplePicker = samplePicker;
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _logger = logger;

        _viewModel.PadMenuRequested += OnPadMenuRequested;
        _viewModel.MenuRequested += OnMenuRequested;
        _viewModel.TutorialPromptChanged += OnTutorialPromptChanged;
        TutorialTip.ActionInvoked += OnTutorialActionInvoked;
        TutorialTip.Closed += OnTutorialClosed;
        _viewModel.TextPrompt = (title, message, initial) => DisplayPromptAsync(title, message, "Save", "Cancel", initialValue: initial, maxLength: 60);
    }
    #endregion

    #region Private methods
    [LoggerMessage(Level = LogLevel.Debug, Message = "Page appeared.")]
    private partial void Log_PageAppeared();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Page disappeared.")]
    private partial void Log_PageDisappeared();

    ///<summary>
    ///Lines the first-run tip's anchor up with the top bar's show/hide button.
    ///</summary>
    private void AlignTipAnchor() => AlignAnchor(TipAnchor, ToggleInfoButtonId);

    ///<summary>
    ///Lines an anchor up under a top bar button. Those buttons are toolbar items, which have no element for a tip to point at, so on
    ///Android the button's native view is found by its content description, which MAUI sets to the item's AutomationId. Where it
    ///can't be found the anchor stays where it is.
    ///</summary>
    private static void AlignAnchor(BoxView anchor, string automationId)
    {
#if ANDROID
        if (Platform.CurrentActivity?.Window?.DecorView is not { } decor
            || FindByDescription(decor, automationId) is not { } button
            || anchor.Handler?.PlatformView is not Android.Views.View view
            || view.Context is not { } context)
        {
            return;
        }

        int[] buttonAt = new int[2];
        int[] viewAt = new int[2];
        button.GetLocationInWindow(buttonAt);
        view.GetLocationInWindow(viewAt);

        // Where the anchor would be centered with no translation, so this can be called again for another button.
        double restingCenter = context.FromPixels(viewAt[0] + (view.Width / 2)) - anchor.TranslationX;
        anchor.TranslationX = context.FromPixels(buttonAt[0] + (button.Width / 2)) - restingCenter;
#endif
    }

#if ANDROID
    private static Android.Views.View? FindByDescription(Android.Views.View view, string description)
    {
        if (string.Equals(view.ContentDescription, description, StringComparison.Ordinal))
        {
            return view;
        }

        if (view is Android.Views.ViewGroup group)
        {
            for (int index = 0; index < group.ChildCount; index++)
            {
                if (group.GetChildAt(index) is { } child && FindByDescription(child, description) is { } found)
                {
                    return found;
                }
            }
        }

        return null;
    }
#endif

    ///<summary>
    ///Scales the device's gaps and text to the size of its cells, so it looks the same on a phone and a tablet. The device fills
    ///the page, so its cells are rectangles: the text is sized from the middle of a cell's width and height, then held to what
    ///fits the cell's width (long words such as Sequencer break otherwise) and its height.
    ///</summary>
    private void OnDeviceHostSizeChanged(object? sender, EventArgs e)
    {
        if (DeviceHost.Width <= 0 || DeviceHost.Height <= 0)
        {
            return;
        }

        // The size of a square device whose cells are as big as these (their geometric mean); the scale below was worked out for one.
        double side = Math.Floor(Math.Sqrt((DeviceHost.Width / DeviceColumns) * (DeviceHost.Height / DeviceRowUnits)) * DeviceColumns);
        DeviceBody.Padding = new Thickness(side * 0.014);

        double gap = Math.Max(1, side * 0.0045);
        Resources["KeyGap"] = new Thickness(gap);
        Resources["PadGap"] = new Thickness(gap);

        // A key must fit its text: about 6.8 ems across for the longest main label and 5.5 for the longest word of a second label,
        // and a labeled key one row tall holds an icon or two lines.
        double keyWidth = ((DeviceHost.Width - (2 * DeviceBody.Padding.Left)) / DeviceColumns) - (2 * gap) - 2;
        double keyHeight = ((DeviceHost.Height - (2 * DeviceBody.Padding.Top)) / DeviceRowUnits) - (2 * gap) - 2;
        Resources["KeyFontSize"] = Math.Clamp(Math.Min(Math.Min(side / 80, keyWidth / 6.8), keyHeight / 6), 4, 12);
        Resources["KeySubFontSize"] = Math.Clamp(Math.Min(Math.Min(side / 118, keyWidth / 5.5), keyHeight / 9), 3.5, 8);
        Resources["KeyIconSize"] = Math.Clamp(Math.Min(side / 26, keyHeight * 0.45), 12, 30);
        Resources["KeyChevronSize"] = Math.Clamp(Math.Min(side / 48, keyHeight * 0.3), 8, 16);
        Resources["PadFontSize"] = Math.Clamp(side / 85, 5, 10);
    }

    ///<summary>
    ///Presses the tapped button. The key is the tap gesture's command parameter.
    ///</summary>
    private void OnKeyTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is LaunchpadKeyViewModel key)
        {
            _viewModel.PressKey(key);
        }
    }

    ///<summary>
    ///Offers the guide to what every button does and the tutorials, and runs the choice.
    ///</summary>
    private async void OnLogoTapped(object? sender, TappedEventArgs e)
    {
        string? chosen = await DisplayActionSheetAsync("Guide and tutorials", "Cancel", null, [ButtonGuideChoice, .. _viewModel.Tutorials.Select(tutorial => tutorial.Title)]);
        if (string.Equals(chosen, ButtonGuideChoice, StringComparison.Ordinal))
        {
            await DisplayAlertAsync("Launchpad guide", Guide, "Got it");
        }
        else if (_viewModel.Tutorials.FirstOrDefault(tutorial => string.Equals(tutorial.Title, chosen, StringComparison.Ordinal)) is { } picked)
        {
            await _viewModel.StartTutorialAsync(picked);
        }
    }

    ///<summary>
    ///The view of a button of the ring, or null if there is none. A track button is found by its column.
    ///</summary>
    private VisualElement? FindKey(LaunchpadControl control, int column)
    {
        foreach (Layout layout in new Layout[] { ShiftLayout, TopLayout, LeftLayout, RightLayout, TrackLayout, SetupLayout, FunctionLayout })
        {
            foreach (IView child in layout.Children)
            {
                if (child is VisualElement { BindingContext: LaunchpadKeyViewModel key } view && key.Control == control && (control != LaunchpadControl.Track || key.Column == column))
                {
                    return view;
                }
            }
        }

        return null;
    }

    ///<summary>
    ///The view of a pad (0 to 63), or null if there is none.
    ///</summary>
    private VisualElement? FindPad(int index)
    {
        foreach (IView child in PadGrid.Children)
        {
            if (child is VisualElement { BindingContext: LaunchpadPadViewModel pad } view && pad.Index == index)
            {
                return view;
            }
        }

        return null;
    }

    ///<summary>
    ///The element a tutorial step's tip points at. A top bar button has no element of its own, so the tutorial's anchor is moved
    ///under it.
    ///</summary>
    private VisualElement? ResolveTutorialTarget(LaunchpadTutorialFocus focus)
    {
        if (focus.Toolbar is { } item)
        {
            AlignAnchor(TutorialAnchor, item switch
            {
                LaunchpadToolbarItem.ShowHideInfo => ToggleInfoButtonId,
                LaunchpadToolbarItem.Edit => EditButtonId,
                _ => StopAllButtonId,
            });
            return TutorialAnchor;
        }

        return focus.Key is { } key ? FindKey(key, 0)
            : focus.Track is { } track ? FindKey(LaunchpadControl.Track, track)
            : focus.Pad is { } pad ? FindPad(pad)
            : null;
    }

    ///<summary>
    ///The area, in window coordinates, that covers the pads a step outlines: the tip keeps off it when it can.
    ///</summary>
    private Rect? OutlinedPadArea(LaunchpadTutorialFocus focus)
    {
        Rect? area = null;
        foreach (int index in focus.HighlightedPads)
        {
            if (FindPad(index) is { } pad)
            {
                Rect bounds = TeachingPopover.WindowBounds(pad);
                area = area is { } current ? current.Union(bounds) : bounds;
            }
        }

        return area;
    }

    ///<summary>
    ///Shows the tutorial tip for a step, or takes it away (null: a step is playing, or the tutorial has ended). A step's tip is closed
    ///and reopened, rather than moved, so it eases in next to whatever it points at now.
    ///</summary>
    private async void OnTutorialPromptChanged(object? sender, LaunchpadTutorialPrompt? prompt)
    {
        int version = ++_tutorialPromptVersion;
        _isClosingTutorialTip = true;
        try
        {
            await TutorialTip.CloseAsync();
        }
        finally
        {
            _isClosingTutorialTip = false;
        }

        if (prompt is null || version != _tutorialPromptVersion || !_viewModel.IsTutorialActive || ResolveTutorialTarget(prompt.Focus) is not { } target)
        {
            return;
        }

        _tutorialActionPressed = false;
        TutorialTip.Title = prompt.Title;
        TutorialTip.Message = prompt.Message;
        TutorialTip.ActionText = prompt.ActionText;
        TutorialTip.CloseText = prompt.CloseText;
        TutorialTip.AvoidBounds = OutlinedPadArea(prompt.Focus);
        TutorialTip.Target = target;
        await TutorialTip.ShowAsync();
    }

    ///<summary>
    ///The tip's main button (Show me, Next or Finish) was pressed.
    ///</summary>
    private void OnTutorialActionInvoked(object? sender, EventArgs e)
    {
        _tutorialActionPressed = true;
        _ = _viewModel.ContinueTutorialAsync();
    }

    ///<summary>
    ///The tip closed. Closing it to change step, or from the main button, is part of the tutorial; the Exit button leaves it.
    ///</summary>
    private void OnTutorialClosed(object? sender, EventArgs e)
    {
        if (_isClosingTutorialTip || _tutorialActionPressed)
        {
            _tutorialActionPressed = false;
            return;
        }

        _viewModel.ExitTutorial();
    }

    ///<summary>
    ///Shows a menu the view model asked for, and runs the choice. The menu is closed before the choice runs, so a choice can open
    ///another menu (Projects, then Open).
    ///</summary>
    private async void OnMenuRequested(object? sender, LaunchpadMenuEventArgs e)
    {
        if (_isMenuOpen)
        {
            return;
        }

        string? chosen;
        _isMenuOpen = true;
        try
        {
            chosen = await DisplayActionSheetAsync(e.Title, "Cancel", null, [.. e.Choices.Select(choice => choice.Text)]);
        }
        finally
        {
            _isMenuOpen = false;
        }

        LaunchpadMenuChoice? picked = e.Choices.FirstOrDefault(choice => string.Equals(choice.Text, chosen, StringComparison.Ordinal));
        if (picked is not null)
        {
            await picked.Run();
        }
    }

    ///<summary>
    ///Opens the pad menu when a pad is long-pressed. The sender is the pad's border, whose binding context is the pad.
    ///</summary>
    private void OnPadLongPressed(object? sender, EventArgs e)
    {
        if (sender is Border { BindingContext: LaunchpadPadViewModel pad })
        {
            _viewModel.OpenPadMenuCommand.Execute(pad);
        }
    }

    ///<summary>
    ///Shows the pad menu, unless one is already showing. In edit mode a long-press opens the menu and then releasing the finger
    ///is also a tap, which asks for the menu again; without this the second request would queue a second, stale menu behind the
    ///first.
    ///</summary>
    private async void OnPadMenuRequested(object? sender, LaunchpadPadEventArgs e)
    {
        if (_isMenuOpen)
        {
            return;
        }

        _isMenuOpen = true;
        try
        {
            await ShowPadMenuAsync(e.Pad).ConfigureAwait(true);
        }
        finally
        {
            _isMenuOpen = false;
        }
    }

    ///<summary>
    ///A finger went down on a pad. In Custom mode this starts the pad's sound.
    ///</summary>
    private void OnPadPressed(object? sender, EventArgs e)
    {
        if (sender is Border { BindingContext: LaunchpadPadViewModel pad })
        {
            _viewModel.PadPressed(pad);
        }
    }

    ///<summary>
    ///A finger came off a pad. In Custom mode this ends the pad's sound.
    ///</summary>
    private void OnPadReleased(object? sender, EventArgs e)
    {
        if (sender is Border { BindingContext: LaunchpadPadViewModel pad })
        {
            _viewModel.PadReleased(pad);
        }
    }

    ///<summary>
    ///Taps a pad: what that does depends on the mode, layer and tool. The pad is the tap gesture's command parameter.
    ///</summary>
    private void OnPadTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is LaunchpadPadViewModel pad)
        {
            _viewModel.PadTapped(pad);
        }
    }

    ///<summary>
    ///Shows the action sheet for a pad and runs the chosen action. Only the actions that apply are offered: an empty pad can
    ///only be assigned.
    ///</summary>
    private async Task ShowPadMenuAsync(LaunchpadPadViewModel pad)
    {
        string title = pad.HasClip ? $"Pad {pad.Index + 1}: {pad.Label}" : $"Pad {pad.Index + 1} (empty)";

        List<string> choices = [AssignSampleChoice, pad.HasClip ? SimilarSampleChoice : SuggestSampleChoice, FillColumnChoice, ImportSampleChoice];
        if (pad.HasClip)
        {
            choices.Add(pad.IsLooping ? LoopOffChoice : LoopOnChoice);
            choices.Add(StopPadChoice);
        }

        string? chosen = await DisplayActionSheetAsync(title, "Cancel", pad.HasClip ? ClearPadChoice : null, [.. choices]);

        // The sheet is closed, so a choice may open the next menu (the suggestions) without being taken for a repeat request.
        _isMenuOpen = false;
        switch (chosen)
        {
            case AssignSampleChoice:
                AudioClip? clip = await _samplePicker.PickAsync($"Sound for pad {pad.Index + 1}", "//launchpad");
                if (clip is not null)
                {
                    _viewModel.AssignClip(pad, clip);
                }

                break;
            case ImportSampleChoice:
                await _viewModel.AssignCommand.ExecuteAsync(pad);
                break;
            case SuggestSampleChoice:
            case SimilarSampleChoice:
                await _viewModel.SuggestForPadAsync(pad);
                break;
            case FillColumnChoice:
                await _viewModel.FillColumnAsync(pad);
                break;
            case LoopOnChoice:
            case LoopOffChoice:
                _viewModel.ToggleLoopCommand.Execute(pad);
                break;
            case StopPadChoice:
                _viewModel.StopPadCommand.Execute(pad);
                break;
            case ClearPadChoice:
                _viewModel.ClearPadCommand.Execute(pad);
                break;
            default:
                break;
        }
    }
    #endregion

    #region Protected methods
    protected override void OnAppearing()
    {
        base.OnAppearing();
        Log_PageAppeared();
        _viewModel.WarmUpIdeas();

        // Show the one-time tip once the page has settled in (its entrance animation is 250 ms).
        _ = Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(500), () =>
        {
            AlignTipAnchor();
            _ = LaunchpadTip.ShowOnceAsync();
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Log_PageDisappeared();

        _ = LaunchpadTip.CloseAsync();

        // Leaving the page ends a tutorial, and puts the user's own pads back.
        _viewModel.ExitTutorial();
    }

    ///<summary>
    ///Back closes an open tip before it leaves the page.
    ///</summary>
    protected override bool OnBackButtonPressed()
    {
        if (_viewModel.IsTutorialActive)
        {
            _viewModel.ExitTutorial();
            return true;
        }

        if (LaunchpadTip.IsOpen)
        {
            _ = LaunchpadTip.CloseAsync();
            return true;
        }

        return base.OnBackButtonPressed();
    }
    #endregion
}
