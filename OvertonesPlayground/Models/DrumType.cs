namespace OvertonesPlayground.Models;

/// <summary>
/// Types of percussive instruments that the synthesizer can generate.
/// </summary>
public enum DrumType
{
    /// <summary>A low, pitched kick drum.</summary>
    Kick,

    /// <summary>A snare drum with noise and tonal body.</summary>
    Snare,

    /// <summary>A short hi-hat / cymbal-like sound.</summary>
    HiHat,

    /// <summary>A handclap-like layered noise burst.</summary>
    Clap,

    /// <summary>A sustained bass-style synth percussive sound.</summary>
    Bass,
}
