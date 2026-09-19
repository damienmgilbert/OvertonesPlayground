namespace OvertonesPlayground.Models;

///<summary>
///One step of a sequencer track: whether it sounds and how.
///</summary>
public class LaunchpadStep
{
    #region Public methods
    ///<summary>
    ///Returns an independent copy of this step.
    ///</summary>
    public LaunchpadStep Clone() => (LaunchpadStep)MemberwiseClone();
    #endregion

    #region Public properties
    ///<summary>
    ///Whether the step sounds when the sequencer reaches it.
    ///</summary>
    public bool IsOn { get; set; }

    ///<summary>
    ///How far the step is nudged late, in quarters of a step (0 to 3).
    ///</summary>
    public int MicroStep { get; set; }

    ///<summary>
    ///The chance, in percent, that the step sounds when the sequencer reaches it.
    ///</summary>
    public int Probability { get; set; } = 100;

    ///<summary>
    ///How loud the step is, from 0 to 1.
    ///</summary>
    public double Velocity { get; set; } = 1;
    #endregion
}
