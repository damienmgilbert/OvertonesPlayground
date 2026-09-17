namespace OvertonesPlayground.Models;

/// <summary>The one-shot drum/bass sounds the Drum Synth can generate.</summary>
public enum DrumType
{
    /// <summary>Pitch-swept low thump.</summary>
    Kick,

    /// <summary>Layered tone-and-noise snare body.</summary>
    Snare,

    /// <summary>Short filtered noise burst.</summary>
    HiHat,

    /// <summary>Several overlapping noise bursts.</summary>
    Clap,

    /// <summary>Pitch-swept sustained low tone.</summary>
    Bass,

    /// <summary>Pitch-swept mid-range drum, between Kick and Bass.</summary>
    Tom,

    /// <summary>Longer-decaying variant of <see cref="HiHat"/>.</summary>
    OpenHiHat,

    /// <summary>Short, high-pitched variant of <see cref="Snare"/>.</summary>
    Rimshot,

    /// <summary>Long-decaying filtered noise, cymbal-like.</summary>
    Crash,

    /// <summary>Short filtered noise, shaker/maraca-like.</summary>
    Shaker,
}
