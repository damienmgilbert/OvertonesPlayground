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
    #region Constants
    private const double NarrowWidth = 900;
    private const int SearchDebounceMilliseconds = 250;
    #endregion

    #region Fields
    private bool _detailOpen;
    private CancellationTokenSource? _searchDebounce;
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
        SizeChanged += (_, _) => ApplyResponsiveLayout();
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

    private void OnDetailUseClicked(object? sender, EventArgs e) => _viewModel.UseSampleCommand.Execute(null);

    private void OnDetailPreviewClicked(object? sender, EventArgs e) => _viewModel.TogglePreviewCommand.Execute(null);

    private void OnDetailSimilarClicked(object? sender, EventArgs e) => _viewModel.FindSimilarCommand.Execute(null);

    private void OnPreviewClicked(object? sender, EventArgs e) => Run<SampleRowViewModel>(sender, _viewModel.TogglePreviewCommand);

    private void OnRelatedTapped(object? sender, TappedEventArgs e) => Run<RelatedSampleViewModel>(sender, _viewModel.ShowRelatedCommand);

    private void OnRowTapped(object? sender, TappedEventArgs e)
    {
        Run<SampleRowViewModel>(sender, _viewModel.SelectRowCommand);
        _detailOpen = true;
        ApplyResponsiveLayout();
    }

    private void OnDetailBackClicked(object? sender, EventArgs e)
    {
        _detailOpen = false;
        ApplyResponsiveLayout();
    }

    ///<summary>
    ///Waits for a pause in typing before searching, so each keystroke doesn't rebuild the results list.
    ///</summary>
    private async void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _searchDebounce?.Cancel();
        _searchDebounce = new CancellationTokenSource();
        CancellationToken token = _searchDebounce.Token;
        string text = e.NewTextValue ?? string.Empty;
        try
        {
            await Task.Delay(SearchDebounceMilliseconds, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (!token.IsCancellationRequested && _viewModel.SearchText != text)
        {
            _viewModel.SearchText = text;
        }
    }

    ///<summary>
    ///On a wide screen the detail panel sits beside the results; on a narrow one it takes over the whole page while a sound is open.
    ///</summary>
    private void ApplyResponsiveLayout()
    {
        bool narrow = Width > 0 && Width < NarrowWidth;
        Root.ColumnDefinitions[1].Width = narrow ? new GridLength(0) : new GridLength(440);
        Grid.SetColumn(DetailScroll, narrow ? 0 : 1);
        Grid.SetColumnSpan(DetailScroll, narrow ? 2 : 1);
        DetailScroll.IsVisible = !narrow || _detailOpen;
        DetailBack.IsVisible = narrow;
    }

    private void OnToggleFiltersClicked(object? sender, EventArgs e) => _viewModel.IsFilterPanelVisible = !_viewModel.IsFilterPanelVisible;
    #endregion

    #region Protected methods
    ///<inheritdoc/>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
        ApplyResponsiveLayout();
    }

    ///<inheritdoc/>
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.StopPreviewCommand.Execute(null);
        _viewModel.AbandonPick();
    }
    #endregion
}
