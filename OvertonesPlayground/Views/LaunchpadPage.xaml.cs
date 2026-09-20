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
    private const double DeviceRowUnits = 10.05;

    private const string AssignSampleChoice = "Assign sample from Sound Bank...";
    private const string ImportSampleChoice = "Import from this device...";
    private const string ClearPadChoice = "Clear pad";
    private const string LoopOffChoice = "Loop: turn off";
    private const string LoopOnChoice = "Loop: turn on";
    private const string StopPadChoice = "Stop this pad";

    private const string Guide = """
        TOP ROW
        Shift: press it, then a button, to use the small label under that button's name.
        Left / Right arrows: previous / next pad bank (A to D).
        Session: each pad launches its sample.
        Note: the pads play the last sample you played as a scale, over two octaves.
        Chord: the pads play chords built on that sample.
        Custom: each pad sounds only while you hold it.
        Sequencer: a 4-track, 32-step sequencer. The top four rows are steps for the chosen track; tap a pad below to give the track a sample.
        Projects: save, open or start a project. Shift + Projects saves the current one.

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
        Scale for Note and Chord, reset the mixer, clear a bank or the sequencer.

        TOP BAR
        Eye: hides or shows the hints and messages floating over the pads. Pencil: edit mode. Square: stops every sound.
        """;
    #endregion

    #region Fields
    private readonly ILogger<LaunchpadPage> _logger;
    private readonly ISamplePickerService _samplePicker;
    private readonly LaunchpadViewModel _viewModel;
    private bool _isMenuOpen;
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
        _viewModel.TextPrompt = (title, message, initial) => DisplayPromptAsync(title, message, "Save", "Cancel", initialValue: initial, maxLength: 60);
    }
    #endregion

    #region Private methods
    [LoggerMessage(Level = LogLevel.Debug, Message = "Page appeared.")]
    private partial void Log_PageAppeared();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Page disappeared.")]
    private partial void Log_PageDisappeared();

    ///<summary>
    ///Lines the tip's anchor up with the top bar's show/hide button. That button is a toolbar item, which has no element for the
    ///tip to point at, so on Android its native view is found by the name it announces. Where it can't be found the anchor stays
    ///where the button usually is.
    ///</summary>
    private void AlignTipAnchor()
    {
#if ANDROID
        if (Platform.CurrentActivity?.Window?.DecorView is not { } decor
            || FindByDescription(decor, _viewModel.TextPanelText) is not { } button
            || TipAnchor.Handler?.PlatformView is not Android.Views.View anchor
            || anchor.Context is not { } context)
        {
            return;
        }

        int[] buttonAt = new int[2];
        int[] anchorAt = new int[2];
        button.GetLocationInWindow(buttonAt);
        anchor.GetLocationInWindow(anchorAt);
        TipAnchor.TranslationX += context.FromPixels(buttonAt[0] + (button.Width / 2)) - context.FromPixels(anchorAt[0] + (anchor.Width / 2));
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
    ///Shows the guide to what every button does.
    ///</summary>
    private async void OnLogoTapped(object? sender, TappedEventArgs e) => await DisplayAlertAsync("Launchpad guide", Guide, "Got it");

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

        List<string> choices = [AssignSampleChoice, ImportSampleChoice];
        if (pad.HasClip)
        {
            choices.Add(pad.IsLooping ? LoopOffChoice : LoopOnChoice);
            choices.Add(StopPadChoice);
        }

        string? chosen = await DisplayActionSheetAsync(title, "Cancel", pad.HasClip ? ClearPadChoice : null, [.. choices]);
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
    }

    ///<summary>
    ///Back closes an open tip before it leaves the page.
    ///</summary>
    protected override bool OnBackButtonPressed()
    {
        if (LaunchpadTip.IsOpen)
        {
            _ = LaunchpadTip.CloseAsync();
            return true;
        }

        return base.OnBackButtonPressed();
    }
    #endregion
}
