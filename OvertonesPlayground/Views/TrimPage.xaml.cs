using System.ComponentModel;
using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

///<summary>
///Code-behind for the visual Trim page: keeps the waveform drawable in sync with the view model, and turns handle drags
public partial class TrimPage : ContentPage
{
    #region Fields
    private readonly TrimWaveformDrawable _drawable = new();
    private readonly ILogger<TrimPage> _logger;
    private double _panStartSeconds;
    private readonly TrimViewModel _viewModel;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the page and hooks up its waveform drawable.
    ///</summary>
    public TrimPage(TrimViewModel viewModel, ILogger<TrimPage> logger)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _logger = logger;

        WaveformView.Drawable = _drawable;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }
    #endregion

    #region Private methods
    [LoggerMessage(Level = LogLevel.Debug, Message = "Page appeared.")]
    private partial void Log_PageAppeared();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Page disappeared.")]
    private partial void Log_PageDisappeared();

        ///<summary>
///Applies a completed (or in-progress) end-handle drag to the view model, clamped against the start handle.
///</summary>
    private void OnEndHandlePanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panStartSeconds = _viewModel.TrimEndSeconds;
                _viewModel.BeginHandleDrag();
                break;
            case GestureStatus.Running:
                double pixelsPerSecond = _viewModel.PixelsPerSecond;
                if (pixelsPerSecond > 0)
                {
                    double proposed = _panStartSeconds + (e.TotalX / pixelsPerSecond);
                    _viewModel.TrimEndSeconds = Math.Clamp(proposed, _viewModel.TrimStartSeconds, _viewModel.DurationSeconds);
                }

                break;
            default:
                break;
        }
    }

    ///<summary>
    ///Applies a completed (or in-progress) start-handle drag to the view model, clamped against the end handle.
    ///</summary>
    private void OnStartHandlePanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panStartSeconds = _viewModel.TrimStartSeconds;
                _viewModel.BeginHandleDrag();
                break;
            case GestureStatus.Running:
                double pixelsPerSecond = _viewModel.PixelsPerSecond;
                if (pixelsPerSecond > 0)
                {
                    double proposed = _panStartSeconds + (e.TotalX / pixelsPerSecond);
                    _viewModel.TrimStartSeconds = Math.Clamp(proposed, 0, _viewModel.TrimEndSeconds);
                }

                break;
            default:
                break;
        }
    }

    ///<summary>
    ///Redraws the waveform whenever the view model's geometry, selection, or playback position changes.
    ///</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(TrimViewModel.WaveformPeaks):
                _drawable.Peaks = _viewModel.WaveformPeaks;
                break;
            case nameof(TrimViewModel.DurationSeconds):
                _drawable.DurationSeconds = _viewModel.DurationSeconds;
                break;
            case nameof(TrimViewModel.RulerStepSeconds):
                _drawable.RulerStepSeconds = _viewModel.RulerStepSeconds;
                break;
            case nameof(TrimViewModel.TrimStartSeconds):
                _drawable.SelectionStartSeconds = _viewModel.TrimStartSeconds;
                break;
            case nameof(TrimViewModel.TrimEndSeconds):
                _drawable.SelectionEndSeconds = _viewModel.TrimEndSeconds;
                break;
            case nameof(TrimViewModel.Mode):
                _drawable.IsTrimMiddleMode = _viewModel.Mode == Models.TrimMode.TrimMiddle;
                break;
            case nameof(TrimViewModel.PositionSeconds):
                _drawable.PlayheadSeconds = _viewModel.PositionSeconds;
                break;
            default:
                return;
        }

        WaveformView.Invalidate();
    }

    ///<summary>
    ///Keeps the view model's pixel-per-second geometry in sync with the waveform's actual rendered width.
    ///</summary>
    private void OnWaveformContainerSizeChanged(object? sender, EventArgs e)
    {
        if (WaveformContainer.Width > 0)
        {
            _viewModel.ViewportWidth = WaveformContainer.Width;
        }
    }

    ///<summary>
    ///Seeks preview playback to the tapped position along the waveform.
    ///</summary>
    private void OnWaveformTapped(object? sender, TappedEventArgs e)
    {
        Point? position = e.GetPosition(WaveformContainer);
        bool hasPosition = position is not null && WaveformContainer.Width > 0;
        if (hasPosition)
        {
            double fraction = Math.Clamp(position!.Value.X / WaveformContainer.Width, 0, 1);
            _viewModel.SeekToPosition(fraction * _viewModel.DurationSeconds);
        }
    }
    #endregion

    #region Protected methods
    protected override void OnAppearing()
    {
        base.OnAppearing();
        Log_PageAppeared();
        _viewModel.StartTicking(Dispatcher);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Log_PageDisappeared();
        _viewModel.StopTicking();
    }
    #endregion
}
