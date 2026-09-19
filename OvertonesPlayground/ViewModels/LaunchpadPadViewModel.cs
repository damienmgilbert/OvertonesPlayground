using CommunityToolkit.Mvvm.ComponentModel;
using OvertonesPlayground.Models;

namespace OvertonesPlayground.ViewModels;

///<summary>
///View model wrapper around <see cref="LaunchpadPad"/> exposing properties used by the UI and helper commands to modify
///pad assignment and loop state.
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
        OnPropertyChanged(nameof(AccessibleName));
    }

    ///<summary>
    ///Unassigns the pad's sample and resets it to the empty, non-looping state.
    ///</summary>
    public void Clear()
    {
        Pad.ClipPath = null;
        Pad.Label = string.Empty;
        Pad.IsLooping = false;
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(ColorHex));
        OnPropertyChanged(nameof(HasClip));
        OnPropertyChanged(nameof(IsLooping));
        OnPropertyChanged(nameof(AccessibleName));
    }

    public void ToggleLoop()
    {
        Pad.IsLooping = !Pad.IsLooping;
        OnPropertyChanged(nameof(IsLooping));
        OnPropertyChanged(nameof(AccessibleName));
    }
    #endregion

    #region Public properties
    ///<summary>
    ///Mirrors the Launchpad's edit mode, so a pad can show its edit badge without a binding that reaches up to the page's view
    ///model (that kind of binding fails once for every pad created before it is attached to the page).
    ///</summary>
    [ObservableProperty]
    public partial bool IsEditMode { get; set; }

    ///<summary>
    ///Name announced by a screen reader, for example "Pad 3, Kick, looping".
    ///</summary>
    public string AccessibleName => $"Pad {Index + 1}, {Label}{(IsLooping ? ", looping" : string.Empty)}";

    ///<summary>
    ///Display color for the pad; a dimmed color when empty.
    ///</summary>
    public string ColorHex => Pad.HasClip ? Pad.ColorHex : "#3A3A3A";

    ///<summary>
    ///True when a clip has been assigned to the pad.
    ///</summary>
    public bool HasClip => Pad.HasClip;

    ///<summary>
    ///Zero-based pad index within the grid.
    ///</summary>
    public int Index => Pad.Index;

    ///<summary>
    ///Returns whether the pad is set to loop.
    ///</summary>
    public bool IsLooping => Pad.IsLooping;

    ///<summary>
    ///Set when the pad is actively being triggered (used for visuals).
    ///</summary>
    [ObservableProperty]
    public partial bool IsTriggered { get; set; }

    ///<summary>
    ///Display label; returns "Empty" when no clip is assigned.
    ///</summary>
    public string Label => Pad.HasClip ? Pad.Label : "Empty";

    ///<summary>
    ///Underlying model represented by this view model.
    ///</summary>
    public LaunchpadPad Pad { get; }
    #endregion
}
