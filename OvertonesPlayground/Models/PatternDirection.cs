namespace OvertonesPlayground.Models;

///<summary>
///The order a sequencer pattern plays its steps in.
///</summary>
public enum PatternDirection
{
    ///<summary>
    ///First step to last, then round again.
    ///</summary>
    Forward,

    ///<summary>
    ///Last step to first, then round again.
    ///</summary>
    Backward,

    ///<summary>
    ///First to last and back again.
    ///</summary>
    PingPong,

    ///<summary>
    ///A random step each time.
    ///</summary>
    Random,
}
