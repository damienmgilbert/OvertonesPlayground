namespace OvertonesPlayground.Models;

///<summary>
///Every tunable parameter exposed by the drum and bass synthesizer. Not every property applies to every <see
///cref="DrumType"/>; the view model decides which controls to show for a given type.
///</summary>
public class DrumSynthParameters
{
    #region Public methods
    ///<summary>
    ///Creates sensible defaults for a given drum type.
    ///</summary>
    public static DrumSynthParameters CreateDefault(DrumType type)
    {
        return type switch
        {
            DrumType.Kick => new DrumSynthParameters { DurationSeconds = 0.35, StartFrequencyHz = 190, EndFrequencyHz = 40, SweepRate = 18, DecayRate = 9, Amplitude = 0.9, },
            DrumType.Bass => new DrumSynthParameters { DurationSeconds = 0.45, StartFrequencyHz = 120, EndFrequencyHz = 80, SweepRate = 25, DecayRate = 4, Amplitude = 0.8, },
            DrumType.Snare => new DrumSynthParameters { DurationSeconds = 0.2, DecayRate = 18, ToneFrequencyHz = 180, ToneDecayRate = 30, ToneLevel = 0.4, NoiseLevel = 0.9, Amplitude = 1.0, },
            DrumType.HiHat => new DrumSynthParameters { DurationSeconds = 0.08, DecayRate = 40, Amplitude = 0.6, FilterType = FilterType.HighPass, FilterCutoffHz = 6000, FilterResonance = 0.7, },
            DrumType.Clap => new DrumSynthParameters { DurationSeconds = 0.25, DecayRate = 25, Amplitude = 0.5, BurstCount = 4, BurstSpacingSeconds = 0.012, },
            DrumType.Tom => new DrumSynthParameters { DurationSeconds = 0.3, StartFrequencyHz = 220, EndFrequencyHz = 90, SweepRate = 12, DecayRate = 6, Amplitude = 0.85, },
            DrumType.OpenHiHat => new DrumSynthParameters { DurationSeconds = 0.4, DecayRate = 8, Amplitude = 0.5, FilterType = FilterType.HighPass, FilterCutoffHz = 7000, FilterResonance = 0.7, },
            DrumType.Rimshot => new DrumSynthParameters { DurationSeconds = 0.06, DecayRate = 50, ToneFrequencyHz = 900, ToneDecayRate = 60, ToneLevel = 0.8, NoiseLevel = 0.3, Amplitude = 0.9, },
            DrumType.Crash => new DrumSynthParameters { DurationSeconds = 1.5, DecayRate = 2, Amplitude = 0.4, FilterType = FilterType.BandPass, FilterCutoffHz = 6000, FilterResonance = 1.2, },
            DrumType.Shaker => new DrumSynthParameters { DurationSeconds = 0.15, DecayRate = 20, Amplitude = 0.5, FilterType = FilterType.HighPass, FilterCutoffHz = 5000, FilterResonance = 0.7, },
            _ => new DrumSynthParameters(),
        };
    }
    #endregion

    #region Public properties
    ///<summary>
    ///Playback amplitude multiplier (0.0 - 1.0).
    ///</summary>
    public double Amplitude { get; set; } = 0.8;

    // Clap: layered noise bursts
    ///<summary>
    ///Number of noise bursts used for clap-like sounds.
    ///</summary>
    public int BurstCount { get; set; } = 4;

    ///<summary>
    ///Spacing (seconds) between clap bursts.
    ///</summary>
    public double BurstSpacingSeconds { get; set; } = 0.012;

    ///<summary>
    ///Envelope decay rate used to shape the sound's tail.
    ///</summary>
    public double DecayRate { get; set; }

    ///<summary>
    ///Overall length of the generated sound in seconds.
    ///</summary>
    public double DurationSeconds { get; set; }

    ///<summary>
    ///Ending frequency (Hz) for a pitched sweep.
    ///</summary>
    public double EndFrequencyHz { get; set; }

    ///<summary>
    ///Filter cutoff frequency in Hz.
    ///</summary>
    public double FilterCutoffHz { get; set; } = 4000;

    ///<summary>
    ///Filter resonance (Q) value.
    ///</summary>
    public double FilterResonance { get; set; } = 0.7;

    // Post-processing filter, applied to every drum type
    ///<summary>
    ///Type of filter applied after synthesis.
    ///</summary>
    public FilterType FilterType { get; set; } = FilterType.None;

    ///<summary>
    ///Relative level of the noise component for snares/claps.
    ///</summary>
    public double NoiseLevel { get; set; } = 0.9;

    ///<summary>
    ///The sample rate (Hz) used when synthesizing the sound.
    ///</summary>
    public int SampleRate { get; set; } = 44_100;

    // Kick / Bass: pitch-sweep shape
    ///<summary>
    ///Starting frequency (Hz) for a pitched sweep.
    ///</summary>
    public double StartFrequencyHz { get; set; }

    ///<summary>
    ///Rate controlling how the frequency sweep progresses.
    ///</summary>
    public double SweepRate { get; set; }

    ///<summary>
    ///Decay rate for the tonal body component.
    ///</summary>
    public double ToneDecayRate { get; set; } = 30;

    // Snare: tonal body layered under the noise
    ///<summary>
    ///Tone oscillator frequency (Hz) used for snare body.
    ///</summary>
    public double ToneFrequencyHz { get; set; } = 180;

    ///<summary>
    ///Level of the tonal body relative to the noise component.
    ///</summary>
    public double ToneLevel { get; set; } = 0.4;
    #endregion
}
