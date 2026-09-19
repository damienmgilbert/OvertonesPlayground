namespace OvertonesPlayground.Models;

///<summary>
///Every button in the ring around the Launchpad's 8 x 8 pads, named after the Novation Launchpad Pro MK3 button it copies.
///</summary>
public enum LaunchpadControl
{
    ///<summary>
    ///Top-left. Makes the next button press use its second function (the small label under the main one).
    ///</summary>
    Shift,

    ///<summary>
    ///Previous pad bank.
    ///</summary>
    Left,

    ///<summary>
    ///Next pad bank.
    ///</summary>
    Right,

    ///<summary>
    ///Grid mode: the pads launch their samples.
    ///</summary>
    Session,

    ///<summary>
    ///Grid mode: the pads play one sample as a scale.
    ///</summary>
    Note,

    ///<summary>
    ///Grid mode: the pads play chords built from one sample.
    ///</summary>
    Chord,

    ///<summary>
    ///Grid mode: the pads play only while held.
    ///</summary>
    Custom,

    ///<summary>
    ///Grid mode: a 4-track, 32-step sequencer.
    ///</summary>
    Sequencer,

    ///<summary>
    ///Saves, opens and starts projects (whole pad and sequencer layouts).
    ///</summary>
    Projects,

    ///<summary>
    ///Transposes everything up a semitone.
    ///</summary>
    Up,

    ///<summary>
    ///Transposes everything down a semitone.
    ///</summary>
    Down,

    ///<summary>
    ///Tool: the next pad or step tapped is cleared.
    ///</summary>
    Clear,

    ///<summary>
    ///Tool: copies a pad, step or pattern onto another. With Shift ("Double") doubles the pattern.
    ///</summary>
    Duplicate,

    ///<summary>
    ///Snaps live pad hits to the tempo grid. With Shift ("Record Quantise") snaps captured hits to steps.
    ///</summary>
    Quantise,

    ///<summary>
    ///Cuts every voice off after a fixed number of beats.
    ///</summary>
    FixedLength,

    ///<summary>
    ///Starts and stops the sequencer.
    ///</summary>
    Play,

    ///<summary>
    ///Turns the last two bars of pad hits into a sequencer pattern.
    ///</summary>
    Capture,

    ///<summary>
    ///Sequencer layer: choose which of the 8 patterns is edited and played.
    ///</summary>
    Patterns,

    ///<summary>
    ///Sequencer layer: turn steps on and off.
    ///</summary>
    Steps,

    ///<summary>
    ///Sequencer layer: the pattern's length, direction and speed.
    ///</summary>
    PatternSettings,

    ///<summary>
    ///Sequencer layer: how loud each step is.
    ///</summary>
    Velocity,

    ///<summary>
    ///Sequencer layer: how likely each step is to sound.
    ///</summary>
    Probability,

    ///<summary>
    ///Randomly changes the pattern.
    ///</summary>
    Mutation,

    ///<summary>
    ///Sequencer layer: nudges each step late by a fraction of a step.
    ///</summary>
    MicroStep,

    ///<summary>
    ///Renders the pattern to an audio clip in the library.
    ///</summary>
    PrintToClip,

    ///<summary>
    ///One of the eight buttons under the pads; acts on the pad column (or sequencer track) above it.
    ///</summary>
    Track,

    ///<summary>
    ///Column function: arms columns for Capture. With Shift ("Undo") undoes the last edit.
    ///</summary>
    RecordArm,

    ///<summary>
    ///Column function: mutes columns. With Shift ("Radio") lets only one pad per column sound at a time.
    ///</summary>
    Mute,

    ///<summary>
    ///Column function: solos columns. With Shift ("Click") turns the metronome on and off.
    ///</summary>
    Solo,

    ///<summary>
    ///Column function: a volume fader per column. With Shift ("&#8226;") the master volume.
    ///</summary>
    Volume,

    ///<summary>
    ///Column function: a pan control per column. With Shift ("&#8226;&#8226;") the master pan.
    ///</summary>
    Pan,

    ///<summary>
    ///Column function: an echo send per column. With Shift ("Tap") tap tempo.
    ///</summary>
    Sends,

    ///<summary>
    ///Column function: a speed control per column. With Shift ("Tempo") the tempo.
    ///</summary>
    Device,

    ///<summary>
    ///Column function: stops columns. With Shift ("Swing") the swing amount.
    ///</summary>
    StopClip,

    ///<summary>
    ///Bottom-left. Opens the setup menu.
    ///</summary>
    Setup,
}
