namespace OvertonesPlayground.Models;

///<summary>
///What the row of buttons under the pads and the pads themselves are showing on top of the current mode: a column function
///(bottom-row keys), a shifted function, or a sequencer layer (right-hand keys). Only one is active at a time; pressing its
///key again, or a mode key, goes back to the plain mode.
///</summary>
public enum LaunchpadLayer
{
    ///<summary>
    ///No layer: the plain mode.
    ///</summary>
    None,

    ///<summary>
    ///The track buttons arm and disarm columns for Capture.
    ///</summary>
    RecordArm,

    ///<summary>
    ///The track buttons mute and unmute columns.
    ///</summary>
    Mute,

    ///<summary>
    ///The track buttons solo and unsolo columns.
    ///</summary>
    Solo,

    ///<summary>
    ///The pads are one volume fader per column.
    ///</summary>
    Volume,

    ///<summary>
    ///The pads are one pan control per column.
    ///</summary>
    Pan,

    ///<summary>
    ///The pads are one echo send per column.
    ///</summary>
    Sends,

    ///<summary>
    ///The pads are one speed (pitch) control per column.
    ///</summary>
    Device,

    ///<summary>
    ///The track buttons stop the sounds in a column.
    ///</summary>
    StopClip,

    ///<summary>
    ///The pads are a single master volume fader.
    ///</summary>
    MasterVolume,

    ///<summary>
    ///The pads are a single master pan control.
    ///</summary>
    MasterPan,

    ///<summary>
    ///The pads pick the tempo.
    ///</summary>
    Tempo,

    ///<summary>
    ///The pads pick the swing amount.
    ///</summary>
    Swing,

    ///<summary>
    ///Sequencer: the pads pick the pattern.
    ///</summary>
    Patterns,

    ///<summary>
    ///Sequencer: the pads turn steps on and off.
    ///</summary>
    Steps,

    ///<summary>
    ///Sequencer: the pads set the pattern's length, direction and speed.
    ///</summary>
    PatternSettings,

    ///<summary>
    ///Sequencer: the pads set each step's velocity.
    ///</summary>
    Velocity,

    ///<summary>
    ///Sequencer: the pads set each step's probability.
    ///</summary>
    Probability,

    ///<summary>
    ///Sequencer: the pads set each step's micro-timing.
    ///</summary>
    MicroStep,
}
