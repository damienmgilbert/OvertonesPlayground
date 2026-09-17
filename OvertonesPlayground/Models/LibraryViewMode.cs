namespace OvertonesPlayground.Models;

/// <summary>How the Library page lays out its clips.</summary>
public enum LibraryViewMode
{
    /// <summary>Compact single-column rows.</summary>
    List,

    /// <summary>Two-column cards showing name, duration, storage location, and actions.</summary>
    Detail,

    /// <summary>A grid of small icon tiles, tap to play.</summary>
    Tile,
}
