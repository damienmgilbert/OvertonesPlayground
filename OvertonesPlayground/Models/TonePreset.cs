namespace OvertonesPlayground.Models;

///<summary>
///A quick-select starting point for the Tone Generator's Wave/Frequency/Duration/Level knobs.
///</summary>
///<param name="Name">Display name shown on the preset button.</param>
///<param name="Waveform">Oscillator/noise type to generate.</param>
///<param name="FrequencyHz">Oscillator frequency in Hz (ignored for noise waveforms).</param>
///<param name="DurationSeconds">Length of the generated tone in seconds.</param>
///<param name="Amplitude">Output level from 0 (silent) to 1 (full scale).</param>
public record TonePreset(string Name, WaveformType Waveform, double FrequencyHz, double DurationSeconds, double Amplitude)
{
    #region Public properties
    ///<summary>
    ///The full catalog of presets shown on the Tone Generator page.
    ///</summary>
    public static IReadOnlyList<TonePreset> All
    {
        get;
    } =
        [
            new("A4 Concert Pitch", WaveformType.Sine, 440, 1.5, 0.8),
            new("Sub Bass", WaveformType.Sine, 55, 2.0, 0.9),
            new("High Chime", WaveformType.Sine, 2000, 0.6, 0.6), new("Soft Pad", WaveformType.Triangle, 220, 2.0, 0.5), new("Alarm Beep", WaveformType.Square, 880, 0.3, 0.9), new(
                                                                                                                                                                                                                                                                                          "Click",
                                                                                                                                                                                                                                                                                          WaveformType.Square,
                                                                                                                                                                                                                                                                                          3000,
                                                                                                                                                                                                                                                                                          0.05,
                                                                                                                                                                                                                                                                                          0.9), new(
                                                                                                                                                                                                                                                                                                "Laser Zap",
                                                                                                                                                                                                                                                                                                WaveformType.Sawtooth,
                                                                                                                                                                                                                                                                                                1500,
                                                                                                                                                                                                                                                                                                0.2,
                                                                                                                                                                                                                                                                                                0.8), new(
                                                                                                                                                                                                                                                                                                      "Buzz",
                                                                                                                                                                                                                                                                                                      WaveformType.Sawtooth,
                                                                                                                                                                                                                                                                                                      110,
                                                                                                                                                                                                                                                                                                      1.0,
                                                                                                                                                                                                                                                                                                      0.7), new(
                                                                                                                                                                                                                                                                                                            "White Hiss",
                                                                                                                                                                                                                                                                                                            WaveformType.WhiteNoise,
                                                                                                                                                                                                                                                                                                            0,
                                                                                                                                                                                                                                                                                                            1.0,
                                                                                                                                                                                                                                                                                                            0.4), new(
                                                                                                                                                                                                                                                                                                                  "Pink Hiss",
                                                                                                                                                                                                                                                                                                                  WaveformType.PinkNoise,
                                                                                                                                                                                                                                                                                                                  0,
                                                                                                                                                                                                                                                                                                                  1.0,
                                                                                                                                                                                                                                                                                                                  0.4), ];
    #endregion
}
