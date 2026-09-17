namespace OvertonesPlayground.Models;

/// <summary>
/// Every tunable knob the Drum &amp; Bass Synth exposes. Not every field applies to every
/// <see cref="DrumType"/> - the ViewModel's DrumSynthParametersViewModel decides which
/// sections are shown for which type.
/// </summary>
public class DrumSynthParameters
{
    public int SampleRate { get; set; } = 44_100;

    public double DurationSeconds { get; set; }

    public double Amplitude { get; set; } = 0.8;

    public double DecayRate { get; set; }

    // Kick / Bass: pitch-sweep shape
    public double StartFrequencyHz { get; set; }

    public double EndFrequencyHz { get; set; }

    public double SweepRate { get; set; }

    // Snare: tonal body layered under the noise
    public double ToneFrequencyHz { get; set; } = 180;

    public double ToneDecayRate { get; set; } = 30;

    public double ToneLevel { get; set; } = 0.4;

    public double NoiseLevel { get; set; } = 0.9;

    // Clap: layered noise bursts
    public int BurstCount { get; set; } = 4;

    public double BurstSpacingSeconds { get; set; } = 0.012;

    // Post-processing filter, applied to every drum type
    public FilterType FilterType { get; set; } = FilterType.None;

    public double FilterCutoffHz { get; set; } = 4000;

    public double FilterResonance { get; set; } = 0.7;

    public static DrumSynthParameters CreateDefault(DrumType type) => type switch
    {
        DrumType.Kick => new DrumSynthParameters
        {
            DurationSeconds = 0.35,
            StartFrequencyHz = 190,
            EndFrequencyHz = 40,
            SweepRate = 18,
            DecayRate = 9,
            Amplitude = 0.9,
        },
        DrumType.Bass => new DrumSynthParameters
        {
            DurationSeconds = 0.45,
            StartFrequencyHz = 120,
            EndFrequencyHz = 80,
            SweepRate = 25,
            DecayRate = 4,
            Amplitude = 0.8,
        },
        DrumType.Snare => new DrumSynthParameters
        {
            DurationSeconds = 0.2,
            DecayRate = 18,
            ToneFrequencyHz = 180,
            ToneDecayRate = 30,
            ToneLevel = 0.4,
            NoiseLevel = 0.9,
            Amplitude = 1.0,
        },
        DrumType.HiHat => new DrumSynthParameters
        {
            DurationSeconds = 0.08,
            DecayRate = 40,
            Amplitude = 0.6,
            FilterType = FilterType.HighPass,
            FilterCutoffHz = 6000,
            FilterResonance = 0.7,
        },
        DrumType.Clap => new DrumSynthParameters
        {
            DurationSeconds = 0.25,
            DecayRate = 25,
            Amplitude = 0.5,
            BurstCount = 4,
            BurstSpacingSeconds = 0.012,
        },
        _ => new DrumSynthParameters(),
    };
}
