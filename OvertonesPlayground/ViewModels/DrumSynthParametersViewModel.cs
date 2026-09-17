using CommunityToolkit.Mvvm.ComponentModel;
using OvertonesPlayground.Models;

namespace OvertonesPlayground.ViewModels;

///<summary>
///Editable, bindable wrapper around <see cref="DrumSynthParameters"/> for one selected <see cref="Models.DrumType"/>.
///Which sliders are relevant depends on the type - see <see cref="ShowPitchSweep"/>, <see cref="ShowSnareControls"/>,
///<see cref="ShowClapControls"/>.
///</summary>
public partial class DrumSynthParametersViewModel : ObservableObject
{
    #region Constructors
    public DrumSynthParametersViewModel(DrumType drumType) : this(drumType, DrumSynthParameters.CreateDefault(drumType))
    {
    }

    public DrumSynthParametersViewModel(DrumType drumType, DrumSynthParameters defaults)
    {
        DrumType = drumType;
        SampleRate = defaults.SampleRate;
        DurationSeconds = defaults.DurationSeconds;
        Amplitude = defaults.Amplitude;
        DecayRate = defaults.DecayRate;
        StartFrequencyHz = defaults.StartFrequencyHz;
        EndFrequencyHz = defaults.EndFrequencyHz;
        SweepRate = defaults.SweepRate;
        ToneFrequencyHz = defaults.ToneFrequencyHz;
        ToneDecayRate = defaults.ToneDecayRate;
        ToneLevel = defaults.ToneLevel;
        NoiseLevel = defaults.NoiseLevel;
        BurstCount = defaults.BurstCount;
        BurstSpacingSeconds = defaults.BurstSpacingSeconds;
        FilterType = defaults.FilterType;
        FilterCutoffHz = defaults.FilterCutoffHz;
        FilterResonance = defaults.FilterResonance;
    }
    #endregion

    #region Public methods
    ///<summary>
    ///Converts the view model values into a <see cref="DrumSynthParameters"/> instance.
    ///</summary>
    public DrumSynthParameters ToParameters()
    {
        return new()
        {
            SampleRate = SampleRate,
            DurationSeconds = DurationSeconds,
            Amplitude = Amplitude,
            DecayRate = DecayRate,
            StartFrequencyHz = StartFrequencyHz,
            EndFrequencyHz = EndFrequencyHz,
            SweepRate = SweepRate,
            ToneFrequencyHz = ToneFrequencyHz,
            ToneDecayRate = ToneDecayRate,
            ToneLevel = ToneLevel,
            NoiseLevel = NoiseLevel,
            BurstCount = BurstCount,
            BurstSpacingSeconds = BurstSpacingSeconds,
            FilterType = FilterType,
            FilterCutoffHz = FilterCutoffHz,
            FilterResonance = FilterResonance,
        };
    }
    #endregion

    #region Public properties
    [ObservableProperty]
    public partial double Amplitude { get; set; }

    [ObservableProperty]
    public partial int BurstCount { get; set; }

    [ObservableProperty]
    public partial double BurstSpacingSeconds { get; set; }

    [ObservableProperty]
    public partial double DecayRate { get; set; }

    ///<summary>
    ///The drum type this view model is representing.
    ///</summary>
    public DrumType DrumType { get; }

    [ObservableProperty]
    public partial double DurationSeconds { get; set; }

    [ObservableProperty]
    public partial double EndFrequencyHz { get; set; }

    [ObservableProperty]
    public partial double FilterCutoffHz { get; set; }

    ///<summary>
    ///Available filter types for post-processing.
    ///</summary>
    public static IReadOnlyList<FilterType> FilterOptions { get; } = Enum.GetValues<FilterType>();

    [ObservableProperty]
    public partial double FilterResonance { get; set; }

    [ObservableProperty]
    public partial FilterType FilterType { get; set; }

    [ObservableProperty]
    public partial double NoiseLevel { get; set; }

    [ObservableProperty]
    public partial int SampleRate { get; set; }

    ///<summary>
    ///Common sample rate choices presented to the user.
    ///</summary>
    public static IReadOnlyList<int> SampleRateOptions { get; } = [8_000, 11_025, 22_050, 44_100, 48_000];

    ///<summary>
    ///True when clap-specific controls should be shown.
    ///</summary>
    public bool ShowClapControls => DrumType is DrumType.Clap;

    ///<summary>
    ///True when pitch-sweep controls are relevant for the selected drum.
    ///</summary>
    public bool ShowPitchSweep => DrumType is DrumType.Kick or DrumType.Bass or DrumType.Tom;

    ///<summary>
    ///True when snare-specific controls should be shown.
    ///</summary>
    public bool ShowSnareControls => DrumType is DrumType.Snare or DrumType.Rimshot;

    [ObservableProperty]
    public partial double StartFrequencyHz { get; set; }

    [ObservableProperty]
    public partial double SweepRate { get; set; }

    [ObservableProperty]
    public partial double ToneDecayRate { get; set; }

    [ObservableProperty]
    public partial double ToneFrequencyHz { get; set; }

    [ObservableProperty]
    public partial double ToneLevel { get; set; }
    #endregion
}
