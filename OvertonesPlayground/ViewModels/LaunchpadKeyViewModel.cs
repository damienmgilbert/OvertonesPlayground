using CommunityToolkit.Mvvm.ComponentModel;
using OvertonesPlayground.Models;

namespace OvertonesPlayground.ViewModels;

///<summary>
///One button in the ring around the pads: what it is called, whether it is lit, and how it is announced.
///</summary>
public partial class LaunchpadKeyViewModel : ObservableObject
{
    #region Constructors
    ///<summary>
    ///Creates a key.
    ///</summary>
    ///<param name="control">Which button it is.</param>
    ///<param name="id">Short name used in its automation id.</param>
    ///<param name="label">The main label. May be empty when the key shows only an icon.</param>
    ///<param name="shiftLabel">The small second label, for the function Shift switches to; null if it has none.</param>
    ///<param name="glyph">The icon, or null for a text-only key.</param>
    ///<param name="description">What the key does, for the tooltip and screen readers.</param>
    ///<param name="position">Where it sits along its row or column of buttons, from 0.</param>
    ///<param name="column">For a track key, which pad column (0 to 7) it belongs to.</param>
    public LaunchpadKeyViewModel(LaunchpadControl control, string id, string label, string? shiftLabel, string? glyph, string description, int position, int column = 0)
    {
        Position = position;
        Control = control;
        Id = id;
        Label = label;
        ShiftLabel = shiftLabel;
        Glyph = glyph;
        Description = description;
        Column = column;
        DisplayGlyph = glyph;
    }
    #endregion

    #region Public properties
    ///<summary>
    ///Name announced by a screen reader.
    ///</summary>
    public string AccessibleName => Control == LaunchpadControl.Track ? $"Column {Column + 1}" : string.IsNullOrEmpty(Label) ? Description : ShiftLabel is null ? Label : $"{Label}, or {ShiftLabel} with Shift";

    ///<summary>
    ///For a track key, which pad column (0 to 7) it belongs to.
    ///</summary>
    public int Column { get; }

    ///<summary>
    ///Which button this is.
    ///</summary>
    public LaunchpadControl Control { get; }

    ///<summary>
    ///What the key does, given as the tooltip and as the screen reader's hint.
    ///</summary>
    public string Description { get; }

    ///<summary>
    ///The icon drawn on the key right now; usually <see cref="Glyph"/>, but Play becomes Stop while the sequencer runs.
    ///</summary>
    [ObservableProperty]
    public partial string? DisplayGlyph { get; set; }

    ///<summary>
    ///The icon the key normally shows, or null for a text-only key.
    ///</summary>
    public string? Glyph { get; }

    ///<summary>
    ///True when the key has an icon to draw.
    ///</summary>
    public bool HasGlyph => !string.IsNullOrEmpty(Glyph);

    ///<summary>
    ///True when the key's icon is the small chevron that sits above a label, drawn smaller than an arrow key's icon.
    ///</summary>
    public bool IsSmallGlyph => HasGlyph && HasLabel;

    ///<summary>
    ///True when the key has a main label.
    ///</summary>
    public bool HasLabel => !string.IsNullOrEmpty(Label);

    ///<summary>
    ///True when the key has a second label.
    ///</summary>
    public bool HasShiftLabel => !string.IsNullOrEmpty(ShiftLabel);

    ///<summary>
    ///Short name used in the key's automation id.
    ///</summary>
    public string Id { get; }

    ///<summary>
    ///Whether the key's light is on.
    ///</summary>
    [ObservableProperty]
    public partial bool IsLit { get; set; }

    ///<summary>
    ///Whether a tutorial is pointing at this button, so it is outlined.
    ///</summary>
    [ObservableProperty]
    public partial bool IsSpotlit { get; set; }

    ///<summary>
    ///Whether Shift is latched, so the key's second label is the one that will run.
    ///</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrimaryOpacity))]
    [NotifyPropertyChangedFor(nameof(SecondaryOpacity))]
    public partial bool IsShifted { get; set; }

    ///<summary>
    ///How strongly the main label is drawn: dimmed while Shift is latched and the key has a second function to switch to.
    ///</summary>
    public double PrimaryOpacity => IsShifted && HasShiftLabel ? 0.4 : 1;

    ///<summary>
    ///How strongly the second label is drawn: full while Shift is latched, muted otherwise.
    ///</summary>
    public double SecondaryOpacity => IsShifted ? 1 : 0.65;

    ///<summary>
    ///The main label.
    ///</summary>
    public string Label { get; }

    ///<summary>
    ///Where the key sits along its row or column of buttons, counting from 0 at the left or the top.
    ///</summary>
    public int Position { get; }

    ///<summary>
    ///The small second label, for the function Shift switches to.
    ///</summary>
    public string? ShiftLabel { get; }
    #endregion
}
