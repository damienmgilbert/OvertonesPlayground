using CommunityToolkit.Maui.Behaviors;

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
        TouchBehavior touch = new() { LongPressDuration = LongPressMilliseconds, };
#pragma warning restore CA2000
        touch.LongPressCompleted += (_, _) => LongPressed?.Invoke(this, EventArgs.Empty);
        Behaviors.Add(touch);
    }
    #endregion

    #region Public events
    /// <summary>
    /// Raised when the border has been pressed and held for the long-press duration.
    /// </summary>
    public event EventHandler? LongPressed;
    #endregion
}
