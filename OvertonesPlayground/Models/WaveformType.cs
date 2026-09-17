namespace OvertonesPlayground.Models;

/// <summary>
/// Common waveform types used by the tone generator.
/// </summary>
public enum WaveformType
{
    /// <summary>A pure sine wave.</summary>
    Sine,

    /// <summary>A square wave with odd harmonics.</summary>
    Square,

    /// <summary>A triangle wave with reduced harmonic content.</summary>
    Triangle,

    /// <summary>A sawtooth wave containing rich harmonic content.</summary>
    Sawtooth,

    /// <summary>White noise source.</summary>
    WhiteNoise,

    /// <summary>Noise with a -3dB/octave roll-off - softer and less hissy than white noise.</summary>
    PinkNoise,
}
