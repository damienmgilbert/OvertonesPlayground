using CommunityToolkit.Mvvm.ComponentModel;
using OvertonesPlayground.Models;

namespace OvertonesPlayground.ViewModels;

public partial class LaunchpadPadViewModel : ObservableObject
{
    public LaunchpadPad Pad { get; }

    [ObservableProperty]
    public partial bool IsTriggered { get; set; }

    public int Index => Pad.Index;

    public string Label => Pad.HasClip ? Pad.Label : "Empty";

    public string ColorHex => Pad.HasClip ? Pad.ColorHex : "#3A3A3A";

    public bool HasClip => Pad.HasClip;

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
