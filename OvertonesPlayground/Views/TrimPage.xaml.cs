using System.ComponentModel;
using OvertonesPlayground.Models;
using OvertonesPlayground.ViewModels;

namespace OvertonesPlayground.Views;

///<summary>
///Code-behind for the visual Trim page: keeps the waveform drawable in sync with the view model, and turns handle
///drags, waveform taps, and time-label taps into <see cref="TrimViewModel"/> updates.
///</summary>
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

    #region Private properties
    ///<summary>
    ///Width of the waveform itself: the container minus the padding that keeps the end handles' touch targets inside its
    ///clip bounds.
    ///</summary>
    private double WaveformWidth => WaveformContainer.Width - WaveformContainer.Padding.HorizontalThickness;
    #endregion

    #region Private methods
    ///<summary>
    ///Applies a completed (or in-progress) end-handle drag to the view model, clamped against the start handle, then
    ///snaps to the nearest zero crossing once the drag ends.
    ///</summary>
    private async void OnEndHandlePanUpdated(object? sender, PanUpdatedEventArgs e)
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
            case GestureStatus.Completed:
                await _viewModel.SnapEndToZeroCrossingAsync();
                break;
            default:
                break;
        }
    }

    ///<summary>
    ///Prompts for an exact end time and applies it, if the entered text parses as a time.
    ///</summary>
    private async void OnEndTimeTapped(object? sender, TappedEventArgs e)
    {
        string? input = await DisplayPromptAsync("Set end time", "Enter time as mm:ss or seconds", initialValue: _viewModel.EndTimeText);
        if (TryParseTime(input, out double seconds))
        {
            _viewModel.SetEndTime(seconds);
        }
    }

    ///<summary>
    ///Shows the library clip picker and, if a clip is chosen, inserts it into the loaded clip at the playhead.
    ///</summary>
    private async void OnInsertClipClicked(object? sender, EventArgs e)
    {
        IReadOnlyList<AudioClip> clips = await _viewModel.GetLibraryClipsAsync();
        AudioClip? chosen = await LibraryClipPicker.PickAsync(this, clips, "Insert clip at playhead");
        if (chosen is not null)
        {
            await _viewModel.InsertClipCommand.ExecuteAsync(chosen);
        }
    }

    ///<summary>
    ///Applies a completed (or in-progress) start-handle drag to the view model, clamped against the end handle, then
    ///snaps to the nearest zero crossing once the drag ends.
    ///</summary>
    private async void OnStartHandlePanUpdated(object? sender, PanUpdatedEventArgs e)
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
            case GestureStatus.Completed:
                await _viewModel.SnapStartToZeroCrossingAsync();
                break;
            default:
                break;
        }
    }

    ///<summary>
    ///Prompts for an exact start time and applies it, if the entered text parses as a time.
    ///</summary>
    private async void OnStartTimeTapped(object? sender, TappedEventArgs e)
    {
        string? input = await DisplayPromptAsync("Set start time", "Enter time as mm:ss or seconds", initialValue: _viewModel.StartTimeText);
        if (TryParseTime(input, out double seconds))
        {
            _viewModel.SetStartTime(seconds);
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
            case nameof(TrimViewModel.WindowStartSeconds):
                _drawable.WindowStartSeconds = _viewModel.WindowStartSeconds;
                break;
            case nameof(TrimViewModel.VisibleSeconds):
                _drawable.VisibleSeconds = _viewModel.VisibleSeconds;
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
        if (WaveformWidth > 0)
        {
            _viewModel.ViewportWidth = WaveformWidth;
        }
    }

    ///<summary>
    ///Seeks preview playback to the tapped position along the waveform.
    ///</summary>
    private void OnWaveformTapped(object? sender, TappedEventArgs e)
    {
        Point? position = e.GetPosition(WaveformContainer);
        bool hasPosition = position is not null && WaveformWidth > 0;
        if (hasPosition)
        {
            double fraction = Math.Clamp((position!.Value.X - WaveformContainer.Padding.Left) / WaveformWidth, 0, 1);
            double seconds = _viewModel.WindowStartSeconds + (fraction * _viewModel.VisibleSeconds);
            _viewModel.SeekToPosition(seconds);
        }
    }

    ///<summary>
    ///Parses a time entered as "mm:ss(.f)" or as plain seconds.
    ///</summary>
    private static bool TryParseTime(string? input, out double seconds)
    {
        seconds = 0;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        string trimmed = input.Trim();
        if (trimmed.Contains(':'))
        {
            string[] parts = trimmed.Split(':');
            double minutes = 0;
            double wholeSeconds = 0;
            bool isValidMinutesSeconds = parts.Length == 2 && double.TryParse(parts[0], out minutes) && double.TryParse(parts[1], out wholeSeconds);
            if (isValidMinutesSeconds)
            {
                seconds = (minutes * 60) + wholeSeconds;
            }

            return isValidMinutesSeconds;
        }

        return double.TryParse(trimmed, out seconds);
    }

    ///<summary>
    ///Repaints the waveform when the theme changes: the drawable reads its colors from the Fluent tokens as it draws.
    ///</summary>
    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e) => WaveformView.Invalidate();
    #endregion

    #region Protected methods
    protected override void OnAppearing()
    {
        base.OnAppearing();
        Application.Current!.RequestedThemeChanged += OnRequestedThemeChanged;
        Log_PageAppeared();
        _viewModel.StartTicking(Dispatcher);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Application.Current!.RequestedThemeChanged -= OnRequestedThemeChanged;
        Log_PageDisappeared();
        _viewModel.StopTicking();
    }
    #endregion

    #region Logging
    [LoggerMessage(Level = LogLevel.Debug, Message = "Page appeared.")]
    private partial void Log_PageAppeared();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Page disappeared.")]
    private partial void Log_PageDisappeared();
    #endregion
}
