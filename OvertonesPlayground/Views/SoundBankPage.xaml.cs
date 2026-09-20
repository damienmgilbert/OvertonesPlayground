using System.Windows.Input;
using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

///<summary>
///Code-behind for the Sound Bank page: loads the catalog when the page first appears, stops any audition when it goes away, and
///forwards taps on chips, rows and related sounds to the view model.
///</summary>
///<remarks>
///Taps are forwarded from here rather than bound with an ancestor <c>RelativeSource</c> to the view model: that binding is
///re-evaluated with no ancestor when a recycled row is rebound, which throws (and MAUI swallows) a
///<see cref="NullReferenceException"/> per row (see <see cref="LibraryPage"/>). The sender inherits the row's, chip's or related
///sound's binding context, so the handler reads the item straight from it.
///</remarks>
public partial class SoundBankPage : ContentPage
{
    #region Fields
    private readonly SoundBankViewModel _viewModel;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the page and binds it to its view model.
    ///</summary>
    public SoundBankPage(SoundBankViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }
    #endregion

    #region Private methods
    private static void Run<T>(object? sender, ICommand command)
        where T : class
    {
        if (sender is BindableObject { BindingContext: T item } && command.CanExecute(item))
        {
            command.Execute(item);
        }
    }

    private void OnChipTapped(object? sender, TappedEventArgs e) => Run<FacetChipViewModel>(sender, _viewModel.ToggleChipCommand);

    private void OnDetailAddClicked(object? sender, EventArgs e) => _viewModel.AddToLibraryCommand.Execute(null);

    private void OnDetailPreviewClicked(object? sender, EventArgs e) => _viewModel.TogglePreviewCommand.Execute(null);

    private void OnDetailSimilarClicked(object? sender, EventArgs e) => _viewModel.FindSimilarCommand.Execute(null);

    private void OnPreviewClicked(object? sender, EventArgs e) => Run<SampleRowViewModel>(sender, _viewModel.TogglePreviewCommand);

    private void OnRelatedTapped(object? sender, TappedEventArgs e) => Run<RelatedSampleViewModel>(sender, _viewModel.ShowRelatedCommand);

    private void OnRowTapped(object? sender, TappedEventArgs e) => Run<SampleRowViewModel>(sender, _viewModel.SelectRowCommand);

    private void OnToggleFiltersClicked(object? sender, EventArgs e) => _viewModel.IsFilterPanelVisible = !_viewModel.IsFilterPanelVisible;
    #endregion

    #region Protected methods
    ///<inheritdoc/>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }

    ///<inheritdoc/>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.StopPreviewCommand.Execute(null);
    }
    #endregion
}
