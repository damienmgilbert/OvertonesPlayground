namespace OvertonesPlayground.Models;

///<summary>
///What the 8 x 8 pads do, chosen with the top-row mode buttons.
///</summary>
public enum LaunchpadMode
{
    ///<summary>
    ///Each pad launches its own sample.
    ///</summary>
    Session,

    ///<summary>
    ///The pads play one sample at different pitches, laid out as a scale.
    ///</summary>
    Note,

    ///<summary>
    ///The pads play chords built on one sample.
    ///</summary>
    Chord,

    ///<summary>
    ///Each pad launches its own sample but only sounds while it is held.
    ///</summary>
    Custom,

    ///<summary>
    ///The pads are the steps of a 4-track step sequencer.
    ///</summary>
    Sequencer,
}
