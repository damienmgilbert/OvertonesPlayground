namespace OvertonesPlayground.Models;

///<summary>
///A button in the top bar that has no view of its own for a tip to point at.
///</summary>
public enum LaunchpadToolbarItem
{
    ///<summary>
    ///The eye, which hides or shows the hints floating over the pads.
    ///</summary>
    ShowHideInfo,

    ///<summary>
    ///The pencil, which turns edit mode on and off.
    ///</summary>
    Edit,

    ///<summary>
    ///The square, which stops every sound.
    ///</summary>
    StopAll,
}

///<summary>
///Where a tutorial step points and what it lights up. One thing is pointed at (a button, a track button, a pad or a top bar
///button) and the tip's tail lands on it; everything named here, the pointed-at thing included, is outlined while the step shows.
///</summary>
public sealed record LaunchpadTutorialFocus
{
    #region Constants
    ///<summary>
    ///The number of pads in the grid.
    ///</summary>
    public const int PadCount = 64;

    private const int Columns = 8;
    #endregion

    #region Public methods
    ///<summary>
    ///Points at a button and outlines it, along with any <paramref name="pads"/>.
    ///</summary>
    public static LaunchpadTutorialFocus OnKey(LaunchpadControl key, params int[] pads) => new() { Key = key, Pads = pads };

    ///<summary>
    ///Points at <paramref name="key"/>, and outlines it, the buttons in <paramref name="also"/> and the <paramref name="pads"/>.
    ///</summary>
    public static LaunchpadTutorialFocus OnKeys(LaunchpadControl key, LaunchpadControl[] also, params int[] pads) => new() { Key = key, Keys = also, Pads = pads };

    ///<summary>
    ///Points at a pad and outlines it and <paramref name="pads"/>.
    ///</summary>
    public static LaunchpadTutorialFocus OnPad(int pad, params int[] pads) => new() { Pad = pad, Pads = pads };

    ///<summary>
    ///Points at a top bar button and outlines the <paramref name="pads"/>.
    ///</summary>
    public static LaunchpadTutorialFocus OnToolbar(LaunchpadToolbarItem item, params int[] pads) => new() { Toolbar = item, Pads = pads };

    ///<summary>
    ///Points at one of the eight buttons under the pads and outlines it, the buttons of <paramref name="alsoTracks"/> and the pads.
    ///</summary>
    public static LaunchpadTutorialFocus OnTrack(int column, int[] alsoTracks, params int[] pads) => new() { Track = column, Tracks = alsoTracks, Pads = pads };

    ///<summary>
    ///The pads of one row (0 to 7), left to right.
    ///</summary>
    public static int[] Row(int row) => [.. Enumerable.Range(row * Columns, Columns)];

    ///<summary>
    ///The pads of the rows <paramref name="first"/> to <paramref name="last"/>, top to bottom.
    ///</summary>
    public static int[] Rows(int first, int last) => [.. Enumerable.Range(first * Columns, (last - first + 1) * Columns)];

    ///<summary>
    ///The pads of one column (0 to 7), top to bottom.
    ///</summary>
    public static int[] Column(int column) => [.. Enumerable.Range(0, Columns).Select(row => (row * Columns) + column)];

    ///<summary>
    ///The pad at <paramref name="row"/> and <paramref name="column"/>.
    ///</summary>
    public static int PadAt(int row, int column) => (row * Columns) + column;
    #endregion

    #region Public properties
    ///<summary>
    ///A button to point at.
    ///</summary>
    public LaunchpadControl? Key { get; init; }

    ///<summary>
    ///More buttons to outline.
    ///</summary>
    public IReadOnlyList<LaunchpadControl> Keys { get; init; } = [];

    ///<summary>
    ///A pad to point at, 0 to 63 counting along each row from the top left.
    ///</summary>
    public int? Pad { get; init; }

    ///<summary>
    ///Pads to outline.
    ///</summary>
    public IReadOnlyList<int> Pads { get; init; } = [];

    ///<summary>
    ///A top bar button to point at.
    ///</summary>
    public LaunchpadToolbarItem? Toolbar { get; init; }

    ///<summary>
    ///One of the eight buttons under the pads to point at, by the column it belongs to.
    ///</summary>
    public int? Track { get; init; }

    ///<summary>
    ///More of the buttons under the pads to outline, by column.
    ///</summary>
    public IReadOnlyList<int> Tracks { get; init; } = [];

    ///<summary>
    ///How many things the step points at: it must be exactly one.
    ///</summary>
    public int PointerCount => new object?[] { Key, Pad, Toolbar, Track }.Count(pointer => pointer is not null);

    ///<summary>
    ///Every button the step outlines: the one it points at and the others.
    ///</summary>
    public IEnumerable<LaunchpadControl> HighlightedKeys => Key is { } key ? [key, .. Keys] : Keys;

    ///<summary>
    ///Every pad the step outlines: the one it points at and the others.
    ///</summary>
    public IEnumerable<int> HighlightedPads => Pad is { } pad ? [pad, .. Pads] : Pads;

    ///<summary>
    ///Every button under the pads that the step outlines, by column.
    ///</summary>
    public IEnumerable<int> HighlightedTracks => Track is { } track ? [track, .. Tracks] : Tracks;
    #endregion
}

///<summary>
///One step of a tutorial: a tip that points at something and says what it is for, and (optionally) the actions the app then
///carries out on the Launchpad so the user sees and hears it working.
///</summary>
///<param name="Title">Short heading of the tip.</param>
///<param name="Message">What the thing is for and why, shown before the step is carried out.</param>
///<param name="Focus">What the tip points at and outlines.</param>
///<param name="Perform">What the app does when the user taps "Show me"; null for a step that only explains.</param>
///<param name="Result">What to notice after <paramref name="Perform"/> has run, shown in the tip's second half; null goes straight to the next step.</param>
public sealed record LaunchpadTutorialStep(string Title, string Message, LaunchpadTutorialFocus Focus, Func<ILaunchpadTutorialHost, Task>? Perform = null, string? Result = null);

///<summary>
///A guided walk through part of the Launchpad, played out on the pads with sounds from the Sound Bank.
///</summary>
///<param name="Id">Stable identifier, for example <c>rhythm</c>.</param>
///<param name="Title">Name shown in the guide menu.</param>
///<param name="Summary">A line on what it teaches.</param>
///<param name="LessonId">The ready-made setup (see <see cref="ILaunchpadTutorialHost.LoadLessonAsync"/>) the tutorial starts on.</param>
///<param name="Steps">The steps, in order.</param>
public sealed record LaunchpadTutorial(string Id, string Title, string Summary, string LessonId, IReadOnlyList<LaunchpadTutorialStep> Steps);

///<summary>
///What the tip shows for the current step, handed to the page to draw.
///</summary>
///<param name="Title">Heading, with the step's place in the tutorial.</param>
///<param name="Message">Text of the tip.</param>
///<param name="ActionText">Label of the button that goes on (Show me, Next or Finish).</param>
///<param name="CloseText">Label of the button that leaves the tutorial.</param>
///<param name="Focus">What the tip points at.</param>
public sealed record LaunchpadTutorialPrompt(string Title, string Message, string ActionText, string CloseText, LaunchpadTutorialFocus Focus);

///<summary>
///What a tutorial step can do to the Launchpad. Each call does what a finger would and returns once the app has had time to show
///and play the result, so a step is a short script: press a button, tap some pads, let it play.
///</summary>
public interface ILaunchpadTutorialHost
{
    #region Methods
    ///<summary>
    ///Puts a sound from the Sound Bank on a pad of the bank on screen, as the pad menu's "Assign sample from Sound Bank" does.
    ///</summary>
    ///<param name="pad">The pad, 0 to 63.</param>
    ///<param name="sampleName">The sound's name in the Sound Bank, without the file extension.</param>
    Task AssignFromSoundBankAsync(int pad, string sampleName);

    ///<summary>
    ///Presses a pad and holds it for a while before letting go; in Custom mode the sound lasts as long as the pad is held.
    ///</summary>
    Task HoldAsync(int pad, int milliseconds);

    ///<summary>
    ///Replaces the pads and sequencer with a ready-made setup built from the Sound Bank for a lesson.
    ///</summary>
    Task LoadLessonAsync(string lessonId);

    ///<summary>
    ///Presses a button, with Shift held first when <paramref name="shifted"/> is true.
    ///</summary>
    Task PressAsync(LaunchpadControl control, bool shifted = false);

    ///<summary>
    ///Presses one of the eight buttons under the pads.
    ///</summary>
    ///<param name="column">The column it belongs to, 0 to 7.</param>
    Task PressTrackAsync(int column);

    ///<summary>
    ///Puts every column's volume, pan, echo and speed, and the master and transpose, back to their starting values.
    ///</summary>
    Task ResetMixerAsync();

    ///<summary>
    ///Turns edit mode on or off.
    ///</summary>
    Task SetEditModeAsync(bool isOn);

    ///<summary>
    ///Chooses the scale Note and Chord modes play in (0 Major, 1 Minor, 2 Pentatonic, 3 Blues, 4 Chromatic).
    ///</summary>
    Task SetScaleAsync(int scaleIndex);

    ///<summary>
    ///Shows pad bank <paramref name="bank"/>, 0 (A) to 3 (D).
    ///</summary>
    Task ShowBankAsync(int bank);

    ///<summary>
    ///Stops every sound and the sequencer.
    ///</summary>
    Task StopAllAsync();

    ///<summary>
    ///Taps pads one after another, waiting <paramref name="gapMilliseconds"/> after each. A negative pad number is a rest.
    ///</summary>
    Task TapAsync(IReadOnlyList<int> pads, int gapMilliseconds);

    ///<summary>
    ///Turns looping on or off for a pad, as the pad menu's Loop choice does.
    ///</summary>
    Task ToggleLoopAsync(int pad);

    ///<summary>
    ///Lets the sound play for a while.
    ///</summary>
    Task WaitAsync(int milliseconds);
    #endregion
}
