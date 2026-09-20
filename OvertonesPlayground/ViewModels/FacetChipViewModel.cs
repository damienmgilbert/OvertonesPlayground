using CommunityToolkit.Mvvm.ComponentModel;

namespace OvertonesPlayground.ViewModels;

///<summary>
///One selectable value of a facet in the Sound Bank filter bar, for example "Kick" or "Mono", with how many sounds have it.
///</summary>
public partial class FacetChipViewModel : ObservableObject
{
    #region Constructors
    ///<summary>
    ///Creates a chip.
    ///</summary>
    ///<param name="key">Identifies the value within its group (a taxonomy key or an enum member name).</param>
    ///<param name="label">Text shown on the chip.</param>
    ///<param name="count">How many sounds have this value; negative hides the count.</param>
    public FacetChipViewModel(string key, string label, int count)
    {
        Key = key;
        Label = label;
        Count = count;
    }
    #endregion

    #region Public properties
    ///<summary>
    ///Number of sounds with this value, or negative when the chip does not show a count.
    ///</summary>
    public int Count { get; }

    ///<summary>
    ///Text shown on the chip: the label, plus the count when there is one.
    ///</summary>
    public string Display => Count < 0 ? Label : $"{Label}  {Count:N0}";

    ///<summary>
    ///Whether the chip is currently applied as a filter.
    ///</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    ///<summary>
    ///Identifies the value within its group.
    ///</summary>
    public string Key { get; }

    ///<summary>
    ///Text shown on the chip.
    ///</summary>
    public string Label { get; }
    #endregion
}
