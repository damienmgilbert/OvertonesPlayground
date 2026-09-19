using AndroidX.AppCompat.Widget;
using Microsoft.Maui.Handlers;
using AView = Android.Views.View;

namespace OvertonesPlayground;

/// <summary>
/// Gives every control that can be pressed a tooltip, so long-pressing anything explains it. A tooltip written on the control
/// (<c>ToolTipProperties.Text</c>) always wins; otherwise the control's spoken description, then its hint, is used. That covers
/// the icon-only buttons and every control whose text comes from a binding (a clip's name, say) without repeating the words as a
/// second attribute, and it keeps the tooltip and what a screen reader announces the same.
/// </summary>
internal static class AutoTooltips
{
    #region Private methods
    private static void Apply(IViewHandler handler, IView view)
    {
        if (view is not VisualElement element || handler.PlatformView is not AView platformView || !Wants(element))
        {
            return;
        }

        // MAUI's own mapping has already set an explicit tooltip; leave it.
        if (!string.IsNullOrEmpty(ToolTipProperties.GetText(element)?.ToString()))
        {
            return;
        }

        string? text = SemanticProperties.GetDescription(element);
        if (string.IsNullOrEmpty(text))
        {
            text = SemanticProperties.GetHint(element);
        }

        if (!string.IsNullOrEmpty(text))
        {
            TooltipCompat.SetTooltipText(platformView, text);
        }
    }

    /// <summary>
    /// The controls that get one: everything you can press, type into or drag, and anything with a gesture on it.
    /// </summary>
    private static bool Wants(VisualElement element) => element is Button or ImageButton or Switch or Slider or Stepper or CheckBox or RadioButton or Picker or DatePicker or TimePicker or Entry or Editor or SearchBar or GraphicsView
        or View { GestureRecognizers.Count: > 0 };
    #endregion

    #region Public methods
    /// <summary>
    /// Hooks the fallback onto every view. Call once, while the app is being built. It runs again whenever a control's
    /// semantic description or hint changes, so a description that arrives through a binding is picked up.
    /// </summary>
    public static void Register() => ViewHandler.ViewMapper.AppendToMapping(nameof(IView.Semantics), Apply);
    #endregion
}
