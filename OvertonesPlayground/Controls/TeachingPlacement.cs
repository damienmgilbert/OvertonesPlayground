namespace OvertonesPlayground.Controls;

/// <summary>
/// Which side of its target a <see cref="TeachingPopover"/> opens on.
/// </summary>
public enum TeachingPlacement
{
    /// <summary>Below the target when the popover fits there, otherwise above it.</summary>
    Auto,

    /// <summary>Below the target, with the tail pointing up at it.</summary>
    Bottom,

    /// <summary>Above the target, with the tail pointing down at it.</summary>
    Top,
}
