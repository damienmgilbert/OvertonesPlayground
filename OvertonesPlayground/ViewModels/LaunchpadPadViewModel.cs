using CommunityToolkit.Mvvm.ComponentModel;
using OvertonesPlayground.Models;

namespace OvertonesPlayground.ViewModels;

/// <summary>
/// View model wrapper around <see cref="LaunchpadPad"/> exposing properties used
/// by the UI and helper commands to modify pad assignment and loop state.
/// </summary>
public partial class LaunchpadPadViewModel : ObservableObject
{
    /// <summary>Underlying model represented by this view model.</summary>
    public LaunchpadPad Pad { get; }

    /// <summary>Set when the pad is actively being triggered (used for visuals).</summary>
    [ObservableProperty]
    public partial bool IsTriggered { get; set; }

    /// <summary>Zero-based pad index within the grid.</summary>
    public int Index => Pad.Index;

    /// <summary>Display label; returns "Empty" when no clip is assigned.</summary>
    public string Label => Pad.HasClip ? Pad.Label : "Empty";

    /// <summary>Display color for the pad; a dimmed color when empty.</summary>
    public string ColorHex => Pad.HasClip ? Pad.ColorHex : "#3A3A3A";

    /// <summary>True when a clip has been assigned to the pad.</summary>
    public bool HasClip => Pad.HasClip;

    /// <summary>Returns whether the pad is set to loop.</summary>
    public bool IsLooping => Pad.IsLooping;

    public LaunchpadPadViewModel(LaunchpadPad pad)
    {
        Pad = pad;
    }

    public void Assign(string clipPath, string label, string colorHex)
    {
        Pad.ClipPath = clipPath;
        Pad.Label = label;
        Pad.ColorHex = colorHex;
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(ColorHex));
        OnPropertyChanged(nameof(HasClip));
    }

    public void ToggleLoop()
    {
        Pad.IsLooping = !Pad.IsLooping;
        OnPropertyChanged(nameof(IsLooping));
    }
}
