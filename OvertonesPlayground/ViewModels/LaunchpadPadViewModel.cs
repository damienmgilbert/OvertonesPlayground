using CommunityToolkit.Mvvm.ComponentModel;
using OvertonesPlayground.Models;

namespace OvertonesPlayground.ViewModels;

///<summary>
///View model wrapper around <see cref="LaunchpadPad"/> exposing what the UI needs: the pad's sample state, and how the pad is
///drawn right now. The 64 pads on screen stay the same objects; switching bank points them at another bank's
///<see cref="LaunchpadPad"/>s, and the Launchpad view model recolors them for the current mode and layer.
///</summary>
public partial class LaunchpadPadViewModel : ObservableObject
{
    #region Constructors
    public LaunchpadPadViewModel(LaunchpadPad pad) { Pad = pad; }
    #endregion

    #region Public methods
    public void Assign(string clipPath, string label, string colorHex)
    {
        Pad.ClipPath = clipPath;
        Pad.Label = label;
        Pad.ColorHex = colorHex;
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(ColorHex));
        OnPropertyChanged(nameof(HasClip));
    }

    ///<summary>
    ///Points this pad at another bank's pad in the same position.
    ///</summary>
    public void Bind(LaunchpadPad pad)
    {
        Pad = pad;
        OnPropertyChanged(nameof(Pad));
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(ColorHex));
        OnPropertyChanged(nameof(HasClip));
        OnPropertyChanged(nameof(IsLooping));
        OnPropertyChanged(nameof(IsLoopBadgeVisible));
    }

    ///<summary>
    ///Unassigns the pad's sample and resets it to the empty, non-looping state.
    ///</summary>
    public void Clear()
    {
        Pad.ClipPath = null;
        Pad.Label = string.Empty;
        Pad.IsLooping = false;
        Pad.Volume = 1;
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(ColorHex));
        OnPropertyChanged(nameof(HasClip));
        OnPropertyChanged(nameof(IsLooping));
        OnPropertyChanged(nameof(IsLoopBadgeVisible));
    }

    public void ToggleLoop()
    {
        Pad.IsLooping = !Pad.IsLooping;
        OnPropertyChanged(nameof(IsLooping));
        OnPropertyChanged(nameof(IsLoopBadgeVisible));
    }
    #endregion

    #region Public properties
    ///<summary>
    ///Name announced by a screen reader, for example "Pad 3, Kick, looping". Set by the Launchpad view model, since what a pad
    ///means depends on the mode.
    ///</summary>
    [ObservableProperty]
    public partial string AccessibleName { get; set; } = string.Empty;

    ///<summary>
    ///The text drawn on the pad: the sample's name in Session mode, a note, step or value in the other modes.
    ///</summary>
    [ObservableProperty]
    public partial string Caption { get; set; } = string.Empty;

    ///<summary>
    ///Color of <see cref="Caption"/>, dark on a lit pad and light on a dim one.
    ///</summary>
    [ObservableProperty]
    public partial string CaptionColorHex { get; set; } = "#CCFFFFFF";

    ///<summary>
    ///The pad's zero-based column in the grid.
    ///</summary>
    public int Column => Index % LaunchpadProject.ColumnCount;

    ///<summary>
    ///Color the pad is lit in right now, as "#AARRGGBB".
    ///</summary>
    [ObservableProperty]
    public partial string DisplayColorHex { get; set; } = "#33FFFFFF";

    ///<summary>
    ///The pad's color as stored with its sample; a dimmed color when empty.
    ///</summary>
    public string ColorHex => Pad.HasClip ? Pad.ColorHex : "#3A3A3A";

    ///<summary>
    ///True when a clip has been assigned to the pad.
    ///</summary>
    public bool HasClip => Pad.HasClip;

    ///<summary>
    ///Zero-based pad index within the grid, counting along each row from the top left.
    ///</summary>
    public int Index => Pad.Index;

    ///<summary>
    ///Mirrors the Launchpad's edit mode, so a pad can show its edit badge without a binding that reaches up to the page's view
    ///model (that kind of binding fails once for every pad created before it is attached to the page).
    ///</summary>
    [ObservableProperty]
    public partial bool IsEditMode { get; set; }

    ///<summary>
    ///Returns whether the pad is set to loop.
    ///</summary>
    public bool IsLooping => Pad.IsLooping;

    ///<summary>
    ///Whether the loop badge is showing: the pad loops and is being drawn as a sample.
    ///</summary>
    public bool IsLoopBadgeVisible => ShowsLoopBadge && IsLooping;

    ///<summary>
    ///Whether the pad is being drawn as a sample, which is when it shows its loop badge.
    ///</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoopBadgeVisible))]
    public partial bool ShowsLoopBadge { get; set; }

    ///<summary>
    ///Display label; returns "Empty" when no clip is assigned.
    ///</summary>
    public string Label => Pad.HasClip ? Pad.Label : "Empty";

    ///<summary>
    ///Underlying model represented by this view model.
    ///</summary>
    public LaunchpadPad Pad { get; private set; }

    ///<summary>
    ///The pad's zero-based row in the grid, from the top.
    ///</summary>
    public int Row => Index / LaunchpadProject.ColumnCount;
    #endregion
}
