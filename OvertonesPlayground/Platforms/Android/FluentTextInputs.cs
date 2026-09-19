using Android.Graphics.Drawables;
using Android.Widget;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using OvertonesPlayground.Themes;
using AColor = Android.Graphics.Color;
using AView = Android.Views.View;
using Color = Microsoft.Maui.Graphics.Color;

namespace OvertonesPlayground;

/// <summary>
/// Draws WinUI's TextBox look on Android: a rounded box with a hairline stroke that turns into a 2 dp accent outline while
/// focused. A MAUI <c>Entry</c>, <c>Editor</c> or <c>Picker</c> has no border or corner properties, so the native
/// background is replaced here. Doing it from the Background mapping means it is rebuilt from the current Fluent tokens
/// every time the style's fill changes, including when the light/dark dictionary is swapped, and it also replaces
/// Android's default underline, whose tint would otherwise stay stuck on the previous theme after a live switch.
/// </summary>
internal static class FluentTextInputs
{
    #region Constants
    private const float CornerRadiusDp = 4;
    private const float FocusedStrokeDp = 2;
    private const int MinimumHorizontalPaddingDp = 12;
    private const float StrokeDp = 1;
    #endregion

    #region Private methods
    ///<summary>
    ///Builds the box for one focus state.
    ///</summary>
    private static GradientDrawable Box(AColor fill, AColor stroke, int strokePx, int cornerPx)
    {
        GradientDrawable box = new();
        box.SetShape(ShapeType.Rectangle);
        box.SetColor(fill);
        box.SetCornerRadius(cornerPx);
        box.SetStroke(strokePx, stroke);
        return box;
    }

    ///<summary>
    ///Replaces the input's background with the Fluent box. The fill is whatever the style (or a visual state such as
    ///Disabled) set, so it follows the tokens; the strokes are read from the active theme.
    ///</summary>
    private static void Apply(AView platformView, IView view)
    {
        if (platformView is not TextView textView || textView.Context is not { } context)
        {
            return;
        }

        float density = context.Resources?.DisplayMetrics?.Density ?? 1f;
        int Px(float dp)
        {
            return (int)MathF.Round(dp * density);
        }

        Color fillColor = view.Background is SolidPaint { Color: { } styled } ? styled : FluentTheme.GetColor("ControlFillColorDefault");
        AColor fill = fillColor.ToPlatform();

        // The drawables are handed to the view, which owns and disposes them, so there is nothing for this method to dispose.
#pragma warning disable CA2000
        GradientDrawable normal = Box(fill, FluentTheme.GetColor("ControlStrokeColorDefault").ToPlatform(), Px(StrokeDp), Px(CornerRadiusDp));
        GradientDrawable focused = Box(fill, FluentTheme.GetColor("AccentFillColorDefault").ToPlatform(), Px(FocusedStrokeDp), Px(CornerRadiusDp));
#pragma warning restore CA2000

        StateListDrawable states = new();
        states.AddState([Android.Resource.Attribute.StateFocused], focused);
        states.AddState([], normal);
        textView.Background = states;

        // The box has no built-in padding, so keep the text off its edges.
        int horizontal = Px(MinimumHorizontalPaddingDp);
        textView.SetPadding(Math.Max(textView.PaddingLeft, horizontal), textView.PaddingTop, Math.Max(textView.PaddingRight, horizontal), textView.PaddingBottom);
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Hooks the Fluent box onto every Entry, Editor and Picker. Call once, while the app is being built.
    ///</summary>
    public static void Register()
    {
        EntryHandler.Mapper.AppendToMapping(nameof(IView.Background), (handler, view) => Apply(handler.PlatformView, view));
        EditorHandler.Mapper.AppendToMapping(nameof(IView.Background), (handler, view) => Apply(handler.PlatformView, view));
        PickerHandler.Mapper.AppendToMapping(nameof(IView.Background), (handler, view) => Apply(handler.PlatformView, view));
    }
    #endregion
}
