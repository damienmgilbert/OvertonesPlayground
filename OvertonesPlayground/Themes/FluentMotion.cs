using Microsoft.Maui.Handlers;

namespace OvertonesPlayground.Themes;

/// <summary>
/// Fluent motion for the whole app, so every animation uses the same durations and easing and honors the same
/// "reduce motion" setting. Durations come from the tokens in <c>FluentMetrics.xaml</c> (faster 83 ms, fast 167 ms, normal
/// 250 ms); MAUI has no key-spline easing, and <see cref="Easing.CubicOut"/> is the closest to Fluent's fast-out, slow-in.
/// <para>
/// Animations here only ever add polish. State that matters is also carried by text, color or an icon, and everything is
/// skipped when Android's "remove animations" setting is on (<see cref="IsReduced"/>).
/// </para>
/// </summary>
public static class FluentMotion
{
    #region Constants
    private const uint FallbackFaster = 83;
    private const uint FallbackFast = 167;
    private const uint FallbackNormal = 250;

    // Names MAUI gives the animations of its ScaleToAsync / FadeToAsync / TranslateToAsync helpers. Aborting by name stops one
    // kind of animation on a view without disturbing the others (a pulsing button can still be pressed).
    private const string FadeAnimation = "FadeTo";
    private const string ScaleAnimation = "ScaleTo";
    private const string TranslateAnimation = "TranslateTo";

    private const double EnterRise = 12;
    private const double PressedScale = 0.97;
    private const uint PulseMilliseconds = 800;
    private const double PulseLowOpacity = 0.6;
    #endregion

    #region Private methods
    private static uint Milliseconds(string token, uint fallback) => Application.Current?.Resources.TryGetValue(token, out object? value) == true && value is int milliseconds ? (uint)milliseconds : fallback;

    private static void OnButtonPressed(object? sender, EventArgs e)
    {
        if (sender is Button button)
        {
            _ = ScaleAsync(button, PressedScale, Faster);
        }
    }

    private static void OnButtonReleased(object? sender, EventArgs e)
    {
        if (sender is Button button)
        {
            _ = ScaleAsync(button, 1, Fast);
        }
    }

    private static Task ScaleAsync(VisualElement view, double scale, uint milliseconds)
    {
        _ = view.AbortAnimation(ScaleAnimation);
        if (IsReduced)
        {
            view.Scale = 1;
            return Task.CompletedTask;
        }

        return view.ScaleToAsync(scale, milliseconds, Easing);
    }
    #endregion

    #region Public methods
    /// <summary>
    /// Slides a page's content up a few dp while fading it in, when the page appears. Ends with the content exactly in place,
    /// and never leaves it hidden if the page is left first (see <see cref="Settle"/>).
    /// </summary>
    public static async Task EnterAsync(VisualElement view)
    {
        ArgumentNullException.ThrowIfNull(view);
        Settle(view);
        if (IsReduced)
        {
            return;
        }

        view.Opacity = 0;
        view.TranslationY = EnterRise;
        _ = await Task.WhenAll(view.FadeToAsync(1, Normal, Easing), view.TranslateToAsync(0, 0, Normal, Easing)).ConfigureAwait(true);
    }

    /// <summary>
    /// Fades a view down and back up, over and over, for as long as <paramref name="isActive"/> says so. Used for a "this is
    /// live" cue such as an active recording; the caller settles the view when it stops.
    /// </summary>
    public static async Task PulseAsync(VisualElement view, Func<bool> isActive)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(isActive);
        while (!IsReduced && isActive())
        {
            // The animation helpers return true when the animation was aborted rather than allowed to finish.
            bool aborted = await view.FadeToAsync(PulseLowOpacity, PulseMilliseconds, Microsoft.Maui.Easing.SinInOut).ConfigureAwait(true);
            if (aborted || !isActive())
            {
                return;
            }

            aborted = await view.FadeToAsync(1, PulseMilliseconds, Microsoft.Maui.Easing.SinInOut).ConfigureAwait(true);
            if (aborted)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Hooks the press feedback onto every <see cref="Button"/>: it shrinks a few percent while pressed and eases back on
    /// release. Call once, while the app is being built.
    /// </summary>
    public static void Register()
    {
        ButtonHandler.Mapper.AppendToMapping(nameof(FluentMotion), static (_, view) =>
        {
            if (view is Button button)
            {
                // Unsubscribing first keeps it to one subscription however many times the mapping runs.
                button.Pressed -= OnButtonPressed;
                button.Pressed += OnButtonPressed;
                button.Released -= OnButtonReleased;
                button.Released += OnButtonReleased;
            }
        });
    }

    /// <summary>
    /// Stops the entrance animations on a view and puts it at rest: fully visible and in place.
    /// </summary>
    public static void Settle(VisualElement view)
    {
        ArgumentNullException.ThrowIfNull(view);
        _ = view.AbortAnimation(FadeAnimation);
        _ = view.AbortAnimation(TranslateAnimation);
        view.Opacity = 1;
        view.TranslationY = 0;
    }
    #endregion

    #region Public properties
    /// <summary>The easing every Fluent animation uses.</summary>
    public static Easing Easing => Microsoft.Maui.Easing.CubicOut;

    /// <summary>Fluent's "fast" duration in milliseconds: the length of most transitions.</summary>
    public static uint Fast => Milliseconds("ControlFastAnimationDuration", FallbackFast);

    /// <summary>Fluent's "faster" duration in milliseconds: presses and things that leave.</summary>
    public static uint Faster => Milliseconds("ControlFasterAnimationDuration", FallbackFaster);

    /// <summary>
    /// True when the user has turned animations off in Android's settings (or with the "remove animations" accessibility
    /// option). Every animation in the app checks this and then does nothing.
    /// </summary>
    public static bool IsReduced =>
#if ANDROID
        Android.Provider.Settings.Global.GetFloat(Android.App.Application.Context.ContentResolver, Android.Provider.Settings.Global.AnimatorDurationScale, 1f) == 0f;
#else
        false;
#endif

    /// <summary>Fluent's "normal" duration in milliseconds: page and panel entrances.</summary>
    public static uint Normal => Milliseconds("ControlNormalAnimationDuration", FallbackNormal);
    #endregion
}
