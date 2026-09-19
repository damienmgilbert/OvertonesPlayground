namespace OvertonesPlayground.Models;

///<summary>
///Mixing settings shared by the eight pads in one column of the grid (and by any sequencer track whose sample came from that
///column).
///</summary>
public class LaunchpadColumn
{
    #region Public properties
    ///<summary>
    ///Whether the column is armed for Capture.
    ///</summary>
    public bool IsArmed { get; set; }

    ///<summary>
    ///Whether the column is silent.
    ///</summary>
    public bool IsMuted { get; set; }

    ///<summary>
    ///Whether the column is soloed. While any column is, only soloed columns sound.
    ///</summary>
    public bool IsSoloed { get; set; }

    ///<summary>
    ///Stereo pan, from -1 (left) to 1 (right).
    ///</summary>
    public double Pan { get; set; }

    ///<summary>
    ///How much of the column's sound is repeated as an echo, from 0 to 1.
    ///</summary>
    public double Send { get; set; }

    ///<summary>
    ///Which of the eight playback speeds the column plays at (0 to 7; see the Launchpad view model for the values).
    ///</summary>
    public int SpeedLevel { get; set; } = 3;

    ///<summary>
    ///Volume, from 0 to 1.
    ///</summary>
    public double Volume { get; set; } = 1;
    #endregion
}
