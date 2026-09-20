using OvertonesPlayground.Models;
using OvertonesPlayground.Themes;
#if ANDROID
using Microsoft.Maui.Platform;
#endif

namespace OvertonesPlayground.Controls;

/// <summary>
/// A coach mark: a small card with a tail that points at one control and explains it, like WinUI's TeachingTip. It opens on
/// the side of the target that has room, lines its tail up with the target's center, eases in with Fluent motion, floats on the
/// overlay shadow, and closes from its button, from a tap anywhere outside it, or when the page calls
/// <see cref="CloseAsync"/> (for example from the Back button). A tip with a <see cref="TipKey"/> is shown only once
/// (<see cref="ShowOnceAsync"/>) and can be brought back with <see cref="TeachingTips.ResetAll"/>.
/// <para>
/// It must be the last child of the page's root <c>Grid</c>, spanning every row and column, so that it sits over the page.
/// Nothing in it takes part in layout or input until it is opened. When it opens, its text is announced to screen readers.
/// </para>
/// </summary>
public partial class TeachingPopover : ContentView
{
    #region Constants
    private const double EdgeMargin = 12;
    private const double EnterScale = 0.96;
    private const int LayoutPollMilliseconds = 25;
    private const int LayoutTimeoutMilliseconds = 1000;
    private const double MaximumWidth = 340;
    private const double TailInset = 18;
    private const double TailOverlap = 1.5;
    private const double TailWidth = 18;
    private const double TargetGap = 2;
    #endregion

    #region Fields
    private bool _isClosing;
    #endregion

    #region Bindable properties
    /// <summary>The label of the button that performs the tip's action. Leave it empty for a tip with only a close button.</summary>
    public static readonly BindableProperty ActionTextProperty = BindableProperty.Create(nameof(ActionText), typeof(string), typeof(TeachingPopover), string.Empty);

    /// <summary>The label of the close button.</summary>
    public static readonly BindableProperty CloseTextProperty = BindableProperty.Create(nameof(CloseText), typeof(string), typeof(TeachingPopover), "Got it");

    /// <summary>The <c>IconFont</c> glyph shown before the title.</summary>
    public static readonly BindableProperty GlyphProperty = BindableProperty.Create(nameof(Glyph), typeof(string), typeof(TeachingPopover), IconFont.Lightbulb);

    /// <summary>Whether a tap outside the popover closes it.</summary>
    public static readonly BindableProperty IsLightDismissEnabledProperty = BindableProperty.Create(nameof(IsLightDismissEnabled), typeof(bool), typeof(TeachingPopover), true, propertyChanged: OnLightDismissChanged);

    /// <summary>An area, in window coordinates, that the popover should keep off when it can: it opens on the side of the target where it covers less of it.</summary>
    public static readonly BindableProperty AvoidBoundsProperty = BindableProperty.Create(nameof(AvoidBounds), typeof(Rect?), typeof(TeachingPopover), null);

    /// <summary>The text of the tip.</summary>
    public static readonly BindableProperty MessageProperty = BindableProperty.Create(nameof(Message), typeof(string), typeof(TeachingPopover), string.Empty);

    /// <summary>Which side of the target the popover opens on.</summary>
    public static readonly BindableProperty PlacementProperty = BindableProperty.Create(nameof(Placement), typeof(TeachingPlacement), typeof(TeachingPopover), TeachingPlacement.Auto);

    /// <summary>The control the tail points at.</summary>
    public static readonly BindableProperty TargetProperty = BindableProperty.Create(nameof(Target), typeof(VisualElement), typeof(TeachingPopover));

    /// <summary>The name this tip is remembered by once seen; without one, <see cref="ShowOnceAsync"/> always shows it.</summary>
    public static readonly BindableProperty TipKeyProperty = BindableProperty.Create(nameof(TipKey), typeof(string), typeof(TeachingPopover), string.Empty);

    /// <summary>The heading of the tip.</summary>
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title), typeof(string), typeof(TeachingPopover), string.Empty);
    #endregion

    #region Constructors
    /// <summary>
    /// Creates a closed popover.
    /// </summary>
    public TeachingPopover()
    {
        InitializeComponent();
    }
    #endregion

    #region Private methods
    private async Task AnimateInAsync()
    {
        if (!FluentMotion.IsReduced)
        {
            Bubble.Scale = EnterScale;
            _ = await Task.WhenAll(Bubble.FadeToAsync(1, FluentMotion.Fast, FluentMotion.Easing), Bubble.ScaleToAsync(1, FluentMotion.Fast, FluentMotion.Easing)).ConfigureAwait(true);
        }

        // However the animation ended, the bubble rests fully visible and at full size.
        Bubble.Opacity = 1;
        Bubble.Scale = 1;
    }

    private static double Overlap(Rect first, Rect second)
    {
        Rect shared = first.Intersect(second);
        return shared.IsEmpty ? 0 : shared.Width * shared.Height;
    }

    private static void OnLightDismissChanged(BindableObject bindable, object oldValue, object newValue) => ((TeachingPopover)bindable).Scrim.InputTransparent = !(bool)newValue;

    private void OnActionClicked(object? sender, EventArgs e)
    {
        ActionInvoked?.Invoke(this, EventArgs.Empty);
        _ = CloseAsync();
    }

    private void OnCloseClicked(object? sender, EventArgs e) => _ = CloseAsync();

    // The page can be resized while the popover is open (rotation, split screen), so it follows its target.
    private void OnOverlaySizeChanged(object? sender, EventArgs e)
    {
        if (IsOpen && !_isClosing && Target is { } target && Overlay.Width > 0)
        {
            Position(target);
        }
    }

    private void OnScrimTapped(object? sender, TappedEventArgs e) => _ = CloseAsync();

    /// <summary>
    /// Puts the bubble next to the target: on the side that has room, clamped to the page's edges, with the tail pointing at
    /// the middle of the target.
    /// </summary>
    private void Position(VisualElement target)
    {
        Rect targetBox = WindowBounds(target);
        Rect overlayBox = WindowBounds(Overlay);
        double targetLeft = targetBox.X - overlayBox.X;
        double targetTop = targetBox.Y - overlayBox.Y;
        double targetBottom = targetTop + targetBox.Height;

        double width = Math.Min(MaximumWidth, Overlay.Width - (2 * EdgeMargin));
        Bubble.WidthRequest = width;
        double height = Bubble.Measure(width, double.PositiveInfinity).Height;

        double left = Math.Clamp(targetLeft + (targetBox.Width / 2) - (width / 2), EdgeMargin, Math.Max(EdgeMargin, Overlay.Width - EdgeMargin - width));
        double tailLeft = Math.Clamp(targetLeft + (targetBox.Width / 2) - left - (TailWidth / 2), TailInset, Math.Max(TailInset, width - TailInset - TailWidth));

        double roomBelow = Overlay.Height - targetBottom - TargetGap - EdgeMargin;
        double roomAbove = targetTop - TargetGap - EdgeMargin;
        bool below = Placement switch
        {
            TeachingPlacement.Bottom => true,
            TeachingPlacement.Top => false,
            TeachingPlacement.Auto or _ => roomBelow >= height || roomBelow >= roomAbove,
        };

        // Given an area to keep clear, and room for the popover on both sides, it opens on the side that covers less of that area.
        if (Placement == TeachingPlacement.Auto && AvoidBounds is { } avoid && roomBelow >= height && roomAbove >= height)
        {
            Rect area = new(avoid.X - overlayBox.X, avoid.Y - overlayBox.Y, avoid.Width, avoid.Height);
            double coveredBelow = Overlap(new Rect(left, targetBottom + TargetGap, width, height), area);
            double coveredAbove = Overlap(new Rect(left, targetTop - TargetGap - height, width, height), area);
            if (coveredBelow != coveredAbove)
            {
                below = coveredBelow < coveredAbove;
            }
        }

        Bubble.HorizontalOptions = LayoutOptions.Start;
        Bubble.AnchorX = (tailLeft + (TailWidth / 2)) / width;
        if (below)
        {
            // The tail is on top of the card, pointing up.
            Bubble.VerticalOptions = LayoutOptions.Start;
            Bubble.Margin = new Thickness(left, targetBottom + TargetGap, 0, 0);
            Grid.SetRow(Tail, 0);
            Grid.SetRow(Card, 1);
            Tail.Rotation = 0;
            Tail.Margin = new Thickness(tailLeft, 0, 0, -TailOverlap);
            Bubble.AnchorY = 0;
        }
        else
        {
            // The tail is underneath the card, pointing down.
            Bubble.VerticalOptions = LayoutOptions.End;
            Bubble.Margin = new Thickness(left, 0, 0, Overlay.Height - targetTop + TargetGap);
            Grid.SetRow(Card, 0);
            Grid.SetRow(Tail, 1);
            Tail.Rotation = 180;
            Tail.Margin = new Thickness(tailLeft, -TailOverlap, 0, 0);
            Bubble.AnchorY = 1;
        }
    }

    /// <summary>
    /// Where an element is inside the app window, in device-independent units. Reading it from the platform view (rather than
    /// adding up the layout offsets) accounts for scrolling, translations and safe-area insets, so the target and the overlay
    /// can be compared directly.
    /// </summary>
    internal static Rect WindowBounds(VisualElement element)
    {
#if ANDROID
        if (element.Handler?.PlatformView is Android.Views.View view && view.Context is { } context)
        {
            int[] location = new int[2];
            view.GetLocationInWindow(location);
            return new Rect(context.FromPixels(location[0]), context.FromPixels(location[1]), element.Width, element.Height);
        }
#endif
        return element.Bounds;
    }

    private static async Task<bool> WaitForLayoutAsync(VisualElement element)
    {
        for (int waited = 0; waited < LayoutTimeoutMilliseconds && element.Width <= 0; waited += LayoutPollMilliseconds)
        {
            await Task.Delay(LayoutPollMilliseconds).ConfigureAwait(true);
        }

        return element.Width > 0;
    }
    #endregion

    #region Public methods
    /// <summary>
    /// Closes the popover, if it is open, and records the tip as seen when it has a <see cref="TipKey"/>.
    /// </summary>
    public async Task CloseAsync()
    {
        if (!IsOpen || _isClosing)
        {
            return;
        }

        _isClosing = true;
        if (!FluentMotion.IsReduced)
        {
            _ = await Bubble.FadeToAsync(0, FluentMotion.Faster, FluentMotion.Easing).ConfigureAwait(true);
        }

        IsVisible = false;
        IsOpen = false;
        _isClosing = false;
        if (TipKey is { Length: > 0 } key)
        {
            TeachingTips.MarkSeen(key);
        }

        Closed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Opens the popover next to its <see cref="Target"/>. Does nothing if it is already open or has no target that has been
    /// laid out.
    /// </summary>
    public async Task ShowAsync()
    {
        if (IsOpen || _isClosing || Target is not { } target || !await WaitForLayoutAsync(target).ConfigureAwait(true))
        {
            return;
        }

        IsOpen = true;
        Bubble.Opacity = 0;
        IsVisible = true;
        if (!await WaitForLayoutAsync(Overlay).ConfigureAwait(true))
        {
            IsVisible = false;
            IsOpen = false;
            return;
        }

        Position(target);
        SemanticScreenReader.Announce($"{Title}. {Message}");
        await AnimateInAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Opens the popover unless it has a <see cref="TipKey"/> and that tip has already been seen.
    /// </summary>
    public async Task ShowOnceAsync()
    {
        if (TipKey is { Length: > 0 } key && TeachingTips.HasSeen(key))
        {
            return;
        }

        await ShowAsync().ConfigureAwait(true);
    }
    #endregion

    #region Public events
    /// <summary>Raised when the action button is pressed, just before the popover closes.</summary>
    public event EventHandler? ActionInvoked;

    /// <summary>Raised after the popover has closed, however it was closed.</summary>
    public event EventHandler? Closed;
    #endregion

    #region Public properties
    public Rect? AvoidBounds
    {
        get => (Rect?)GetValue(AvoidBoundsProperty);
        set => SetValue(AvoidBoundsProperty, value);
    }

    public string ActionText
    {
        get => (string)GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public string CloseText
    {
        get => (string)GetValue(CloseTextProperty);
        set => SetValue(CloseTextProperty, value);
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    /// <summary>Whether the popover is showing (or closing).</summary>
    public bool IsOpen { get; private set; }

    public bool IsLightDismissEnabled
    {
        get => (bool)GetValue(IsLightDismissEnabledProperty);
        set => SetValue(IsLightDismissEnabledProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public TeachingPlacement Placement
    {
        get => (TeachingPlacement)GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    public VisualElement? Target
    {
        get => (VisualElement?)GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    public string TipKey
    {
        get => (string)GetValue(TipKeyProperty);
        set => SetValue(TipKeyProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
    #endregion
}
