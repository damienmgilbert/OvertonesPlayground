using CommunityToolkit.Maui.Behaviors;
using CommunityToolkit.Maui.Core;
using OvertonesPlayground.Themes;

namespace OvertonesPlayground.Controls;

/// <summary>
/// A <see cref="Border"/> that raises <see cref="LongPressed"/> when it is pressed and held. The behavior is added in the
/// constructor, so it is in place before the platform view is created, and the sender of the event is the border itself:
/// inside a <c>DataTemplate</c> its <see cref="BindableObject.BindingContext"/> is therefore the row's item. (A
/// <c>TouchBehavior</c> declared in the template instead gets no binding context, so it can't tell which row was pressed.)
/// </summary>
public class LongPressBorder : Border
{
    #region Constants
    private const int LongPressMilliseconds = 500;
    private const double PressedScale = 0.96;
    #endregion

    #region Constructors
    /// <summary>
    /// Creates the border with a long-press behavior that fires after half a second.
    /// </summary>
    public LongPressBorder()
    {
        // The behavior lives in this border's Behaviors collection for the border's whole lifetime, so there is nothing for
        // the constructor to dispose.
#pragma warning disable CA2000
        TouchBehavior touch = new()
        {
            LongPressDuration = LongPressMilliseconds,
            // The border shrinks a little while pressed, like a Button does (see FluentMotion), unless animations are turned off.
            PressedScale = FluentMotion.IsReduced ? 1 : PressedScale,
            PressedAnimationDuration = (int)FluentMotion.Faster,
            PressedAnimationEasing = FluentMotion.Easing,
            DefaultAnimationDuration = (int)FluentMotion.Fast,
            DefaultAnimationEasing = FluentMotion.Easing,
        };
#pragma warning restore CA2000
        touch.LongPressCompleted += (_, _) => LongPressed?.Invoke(this, EventArgs.Empty);
        touch.CurrentTouchStateChanged += (_, e) =>
        {
            if (e.State == TouchState.Pressed)
            {
                Pressed?.Invoke(this, EventArgs.Empty);
            }
            else if (e.State == TouchState.Default)
            {
                Released?.Invoke(this, EventArgs.Empty);
            }
        };
        Behaviors.Add(touch);
    }
    #endregion

    #region Public events
    /// <summary>
    /// Raised when the border has been pressed and held for the long-press duration.
    /// </summary>
    public event EventHandler? LongPressed;

    /// <summary>
    /// Raised when a finger goes down on the border.
    /// </summary>
    public event EventHandler? Pressed;

    /// <summary>
    /// Raised when the finger comes off the border (or is dragged off it, or the touch is canceled).
    /// </summary>
    public event EventHandler? Released;
    #endregion
}
