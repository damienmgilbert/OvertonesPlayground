namespace OvertonesPlayground.Tests.ViewModels;

public sealed class DrumSynthParametersViewModelTests
{
    #region Public methods
    public static TheoryData<DrumType> AllDrums() => [.. Enum.GetValues<DrumType>()];

    [Theory]
    [MemberData(nameof(AllDrums))]
    public void Constructor_StartsFromThatDrumsDefaults(DrumType drum)
    {
        DrumSynthParameters defaults = DrumSynthParameters.CreateDefault(drum);

        DrumSynthParameters roundTripped = new DrumSynthParametersViewModel(drum).ToParameters();

        foreach (System.Reflection.PropertyInfo property in typeof(DrumSynthParameters).GetProperties().Where(p => p.CanRead))
        {
            Assert.True(Equals(property.GetValue(defaults), property.GetValue(roundTripped)), $"{drum}: {property.Name} changed on the way through the view model");
        }
    }

    [Fact]
    public void Constructor_WithExplicitDefaults_UsesThose()
    {
        DrumSynthParameters custom = DrumSynthParameters.CreateDefault(DrumType.Snare);
        custom.Amplitude = 0.33;
        custom.BurstCount = 7;
        custom.FilterType = FilterType.BandPass;

        DrumSynthParametersViewModel viewModel = new(DrumType.Snare, custom);

        Assert.Equal(0.33, viewModel.Amplitude);
        Assert.Equal(7, viewModel.BurstCount);
        Assert.Equal(FilterType.BandPass, viewModel.FilterType);
        Assert.Equal(DrumType.Snare, viewModel.DrumType);
    }

    [Fact]
    public void TheChoicesOffered_CoverEveryFilterAndTheCommonSampleRates()
    {
        Assert.Equal(Enum.GetValues<FilterType>(), DrumSynthParametersViewModel.FilterOptions);
        Assert.Equal([8_000, 11_025, 22_050, 44_100, 48_000], DrumSynthParametersViewModel.SampleRateOptions);
    }

    [Theory]
    [InlineData(DrumType.Kick, true, false, false)]
    [InlineData(DrumType.Bass, true, false, false)]
    [InlineData(DrumType.Tom, true, false, false)]
    [InlineData(DrumType.Snare, false, true, false)]
    [InlineData(DrumType.Rimshot, false, true, false)]
    [InlineData(DrumType.Clap, false, false, true)]
    [InlineData(DrumType.HiHat, false, false, false)]
    [InlineData(DrumType.OpenHiHat, false, false, false)]
    [InlineData(DrumType.Crash, false, false, false)]
    [InlineData(DrumType.Shaker, false, false, false)]
    public void TheControlsShownDependOnTheDrum(DrumType drum, bool pitchSweep, bool snare, bool clap)
    {
        DrumSynthParametersViewModel viewModel = new(drum);

        Assert.Equal(pitchSweep, viewModel.ShowPitchSweep);
        Assert.Equal(snare, viewModel.ShowSnareControls);
        Assert.Equal(clap, viewModel.ShowClapControls);
    }

    [Fact]
    public void ToParameters_ReflectsEveryEditedValue()
    {
        DrumSynthParametersViewModel viewModel = new(DrumType.Kick)
        {
            SampleRate = 22_050,
            DurationSeconds = 0.9,
            Amplitude = 0.7,
            DecayRate = 11,
            StartFrequencyHz = 150,
            EndFrequencyHz = 40,
            SweepRate = 30,
            ToneFrequencyHz = 200,
            ToneDecayRate = 12,
            ToneLevel = 0.5,
            NoiseLevel = 0.25,
            BurstCount = 4,
            BurstSpacingSeconds = 0.02,
            FilterType = FilterType.HighPass,
            FilterCutoffHz = 900,
            FilterResonance = 2.5,
        };

        DrumSynthParameters parameters = viewModel.ToParameters();

        Assert.Equal(22_050, parameters.SampleRate);
        Assert.Equal(0.9, parameters.DurationSeconds);
        Assert.Equal(0.7, parameters.Amplitude);
        Assert.Equal(11, parameters.DecayRate);
        Assert.Equal(150, parameters.StartFrequencyHz);
        Assert.Equal(40, parameters.EndFrequencyHz);
        Assert.Equal(30, parameters.SweepRate);
        Assert.Equal(200, parameters.ToneFrequencyHz);
        Assert.Equal(12, parameters.ToneDecayRate);
        Assert.Equal(0.5, parameters.ToneLevel);
        Assert.Equal(0.25, parameters.NoiseLevel);
        Assert.Equal(4, parameters.BurstCount);
        Assert.Equal(0.02, parameters.BurstSpacingSeconds);
        Assert.Equal(FilterType.HighPass, parameters.FilterType);
        Assert.Equal(900, parameters.FilterCutoffHz);
        Assert.Equal(2.5, parameters.FilterResonance);
    }
    #endregion
}
