using CommunityToolkit.Mvvm.ComponentModel;
using OvertonesPlayground.Ontology.Querying;

namespace OvertonesPlayground.ViewModels;

///<summary>
///A row of related chips in the Sound Bank filter bar (Kit, Stereo, Length ...). Each group knows how to turn one of its chips into
///a <see cref="SampleSpecification"/>; the page ANDs the specifications of every selected chip.
///</summary>
public partial class FacetGroupViewModel : ObservableObject
{
    #region Fields
    private readonly Func<string, SampleSpecification> _specify;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates a group.
    ///</summary>
    ///<param name="title">Heading of the row.</param>
    ///<param name="chips">The values to choose from.</param>
    ///<param name="specify">Builds the filter for one chip, from its key.</param>
    ///<param name="isMultiSelect">Whether several chips can be on at once (all of them must then match).</param>
    public FacetGroupViewModel(string title, IReadOnlyList<FacetChipViewModel> chips, Func<string, SampleSpecification> specify, bool isMultiSelect = false)
    {
        Title = title;
        Chips = chips;
        _specify = specify;
        IsMultiSelect = isMultiSelect;
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Turns every chip off.
    ///</summary>
    public void Clear()
    {
        foreach (FacetChipViewModel chip in Chips)
        {
            chip.IsSelected = false;
        }
    }

    ///<summary>
    ///The filter for every selected chip of this group.
    ///</summary>
    public IEnumerable<SampleSpecification> Specifications() => Chips.Where(chip => chip.IsSelected).Select(chip => _specify(chip.Key));

    ///<summary>
    ///Turns <paramref name="chip"/> on or off. In a single-select group, turning one on turns the others off.
    ///</summary>
    public void Toggle(FacetChipViewModel chip)
    {
        bool wasSelected = chip.IsSelected;
        if (!IsMultiSelect)
        {
            Clear();
        }

        chip.IsSelected = !wasSelected;
    }
    #endregion

    #region Public properties
    ///<summary>
    ///The values to choose from. For the instrument group this list is replaced as the user drills down.
    ///</summary>
    [ObservableProperty]
    public partial IReadOnlyList<FacetChipViewModel> Chips { get; set; }

    ///<summary>
    ///Whether the group has any chip selected.
    ///</summary>
    public bool HasSelection => Chips.Any(chip => chip.IsSelected);

    ///<summary>
    ///Whether several chips can be selected at once.
    ///</summary>
    public bool IsMultiSelect { get; }

    ///<summary>
    ///Extra text after the title, for example the current instrument path.
    ///</summary>
    [ObservableProperty]
    public partial string Subtitle { get; set; } = string.Empty;

    ///<summary>
    ///Heading of the row.
    ///</summary>
    public string Title { get; }
    #endregion
}
