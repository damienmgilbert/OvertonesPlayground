namespace OvertonesPlayground.Tests.ViewModels;

///<summary>
///The Tone Generator, Text to Speech and Drum Synth pages all follow one flow: generate a preview, play it, then either
///Save it or throw it away. The three share their tests here so the flow is checked the same way on each.
///</summary>
public sealed class GeneratorViewModelTests
{
    #region Fields
    private readonly IAudioEditorService _editor = Substitute.For<IAudioEditorService>();
    private readonly IAudioLibraryService _library = Substitute.For<IAudioLibraryService>();
    private readonly IAudioPlaybackService _playback = Substitute.For<IAudioPlaybackService>();
    private readonly ITextToSpeechService _speech = Substitute.For<ITextToSpeechService>();
    private readonly ISoundSynthesisService _synthesis = Substitute.For<ISoundSynthesisService>();
    #endregion

    #region Private methods
    private DrumSynthViewModel CreateDrum() => new(_synthesis, _editor, _playback, _library, NullLogger<DrumSynthViewModel>.Instance);

    private TextToSpeechViewModel CreateSpeech() => new(_speech, _editor, _playback, _library, NullLogger<TextToSpeechViewModel>.Instance);

    private ToneGeneratorViewModel CreateTone() => new(_synthesis, _editor, _playback, _library, NullLogger<ToneGeneratorViewModel>.Instance);

    private AudioClip PreviewClip(string name = "Preview")
    {
        AudioClip clip = TestData.Clip(name, path: $"/synth/{name}.wav");
        _editor.GetWaveformPeaksAsync(clip.FilePath, 300).Returns([0.3f, 0.6f]);
        return clip;
    }
    #endregion

    #region Public methods
    [Fact]
    public async Task Drum_Generate_SynthesisFails_ReportsIt()
    {
        _synthesis.GenerateDrumAsync(default, default!, default!).ReturnsForAnyArgs(Task.FromException<AudioClip>(new IOException()));
        DrumSynthViewModel viewModel = CreateDrum();

        await viewModel.GenerateCommand.ExecuteAsync(null);

        Assert.Equal("Couldn't generate that drum sample.", viewModel.StatusMessage);
        Assert.False(viewModel.CanSave);
    }

    [Fact]
    public async Task Drum_Generate_SynthesizesTheSelectedDrumWithItsParameters()
    {
        AudioClip clip = PreviewClip("Snare");
        _synthesis.GenerateDrumAsync(DrumType.Snare, "Snare", Arg.Is<DrumSynthParameters>(p => p.Amplitude == 0.42 && p.DurationSeconds == 0.7)).Returns(clip);
        DrumSynthViewModel viewModel = CreateDrum();
        viewModel.SelectDrumCommand.Execute(DrumType.Snare);
        viewModel.SelectedDrumParams.Amplitude = 0.42;
        viewModel.SelectedDrumParams.DurationSeconds = 0.7;

        await viewModel.GenerateCommand.ExecuteAsync(null);

        Assert.True(viewModel.CanSave);
        Assert.Equal([0.3f, 0.6f], viewModel.WaveformPeaks);
        Received.InOrder(
        () =>
        {
            _playback.LoadAsync(clip);
            _playback.Play();
        });
    }

    [Fact]
    public async Task Drum_Save_AddsThePreviewToTheLibrary()
    {
        AudioClip clip = PreviewClip("Kick");
        _synthesis.GenerateDrumAsync(default, default!, default!).ReturnsForAnyArgs(clip);
        _library.AddClipAsync(clip.FilePath, "Kick", true).Returns(TestData.Clip("Kick"));
        DrumSynthViewModel viewModel = CreateDrum();
        await viewModel.GenerateCommand.ExecuteAsync(null);

        await viewModel.SaveCommand.ExecuteAsync(null);

        await _library.Received(1).AddClipAsync(clip.FilePath, "Kick", true);
        Assert.False(viewModel.CanSave);
    }

    [Fact]
    public async Task Drum_SelectDrum_LoadsThatDrumsDefaultsAndDropsTheOldPreview()
    {
        AudioClip preview = PreviewClip("Kick");
        _synthesis.GenerateDrumAsync(default, default!, default!).ReturnsForAnyArgs(preview);
        DrumSynthViewModel viewModel = CreateDrum();
        viewModel.SelectedDrumParams.Amplitude = 0.05;
        await viewModel.GenerateCommand.ExecuteAsync(null);
        Assert.True(viewModel.CanSave);

        viewModel.SelectDrumCommand.Execute(DrumType.HiHat);

        Assert.Equal(DrumType.HiHat, viewModel.SelectedDrumParams.DrumType);
        Assert.Equal(DrumSynthParameters.CreateDefault(DrumType.HiHat).Amplitude, viewModel.SelectedDrumParams.Amplitude);
        Assert.False(viewModel.CanSave);
        Assert.Empty(viewModel.WaveformPeaks);
    }

    [Fact]
    public async Task Drum_SelectDrum_ThenSave_DoesNotSaveTheOldDrumsPreview()
    {
        AudioClip kick = PreviewClip("Kick");
        _synthesis.GenerateDrumAsync(default, default!, default!).ReturnsForAnyArgs(kick);
        _library.AddClipAsync(default!, default!, default).ReturnsForAnyArgs(TestData.Clip("saved"));
        DrumSynthViewModel viewModel = CreateDrum();
        await viewModel.GenerateCommand.ExecuteAsync(null);
        viewModel.SelectDrumCommand.Execute(DrumType.Tom);

        // The Save command has no CanExecute of its own (the page merely disables the button), so the view model has to forget
        // the pending clip itself when the drum changes.
        await viewModel.SaveCommand.ExecuteAsync(null);

        await _library.DidNotReceiveWithAnyArgs().AddClipAsync(default!, default!, default);
    }

    [Fact]
    public void Drum_StartsWithAKick()
    {
        DrumSynthViewModel viewModel = CreateDrum();

        Assert.Equal("Drum Synth", viewModel.Title);
        Assert.Equal(DrumType.Kick, viewModel.SelectedDrumParams.DrumType);
        Assert.False(viewModel.CanSave);
        Assert.Equal(Enum.GetValues<DrumType>(), viewModel.DrumOptions);
    }

    [Fact]
    public async Task Speech_Generate_LongTextGetsAShortenedName()
    {
        string text = new('a', 60);
        AudioClip preview = PreviewClip();
        _speech.SynthesizeAsync(default!, default!).ReturnsForAnyArgs(preview);
        TextToSpeechViewModel viewModel = CreateSpeech();
        viewModel.Text = text;

        await viewModel.GenerateCommand.ExecuteAsync(null);

        await _speech.Received(1).SynthesizeAsync(text, $"{new string('a', 40)}...");
    }

    [Fact]
    public async Task Speech_Generate_SynthesisFails_ReportsIt()
    {
        _speech.SynthesizeAsync(default!, default!).ReturnsForAnyArgs(Task.FromException<AudioClip>(new NotSupportedException()));
        TextToSpeechViewModel viewModel = CreateSpeech();
        viewModel.Text = "Hello";

        await viewModel.GenerateCommand.ExecuteAsync(null);

        Assert.Equal("Couldn't generate that speech clip.", viewModel.StatusMessage);
        Assert.False(viewModel.CanSave);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task Speech_Generate_SynthesizesPreviewsAndEnablesSave()
    {
        AudioClip clip = PreviewClip("Hello there");
        _speech.SynthesizeAsync("Hello there", "Hello there").Returns(clip);
        TextToSpeechViewModel viewModel = CreateSpeech();
        viewModel.Text = "Hello there";

        await viewModel.GenerateCommand.ExecuteAsync(null);

        Assert.True(viewModel.CanSave);
        Assert.Equal([0.3f, 0.6f], viewModel.WaveformPeaks);
        Assert.Equal("Previewing - tap Save to keep it in your library.", viewModel.StatusMessage);
        Received.InOrder(
        () =>
        {
            _playback.LoadAsync(clip);
            _playback.Play();
        });
    }

    [Fact]
    public async Task Speech_Generate_TextExactlyAtTheLimitIsNotShortened()
    {
        string text = new('b', 40);
        AudioClip preview = PreviewClip();
        _speech.SynthesizeAsync(default!, default!).ReturnsForAnyArgs(preview);
        TextToSpeechViewModel viewModel = CreateSpeech();
        viewModel.Text = text;

        await viewModel.GenerateCommand.ExecuteAsync(null);

        await _speech.Received(1).SynthesizeAsync(text, text);
    }

    [Fact]
    public void Speech_GenerateIsOnlyAvailableOnceThereIsText()
    {
        TextToSpeechViewModel viewModel = CreateSpeech();
        Assert.False(viewModel.GenerateCommand.CanExecute(null));

        viewModel.Text = "  ";
        Assert.False(viewModel.GenerateCommand.CanExecute(null));

        viewModel.Text = "Hello";
        Assert.True(viewModel.GenerateCommand.CanExecute(null));
    }

    [Fact]
    public async Task Speech_Save_AddsThePreviewToTheLibrary()
    {
        AudioClip clip = PreviewClip("Hello");
        _speech.SynthesizeAsync(default!, default!).ReturnsForAnyArgs(clip);
        _library.AddClipAsync(clip.FilePath, "Hello", true).Returns(TestData.Clip("Hello"));
        TextToSpeechViewModel viewModel = CreateSpeech();
        viewModel.Text = "Hello";
        await viewModel.GenerateCommand.ExecuteAsync(null);

        await viewModel.SaveCommand.ExecuteAsync(null);

        await _library.Received(1).AddClipAsync(clip.FilePath, "Hello", true);
        Assert.False(viewModel.CanSave);
        Assert.Equal("Saved 'Hello' to your library.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Speech_Save_NothingGeneratedYet_DoesNothing()
    {
        await CreateSpeech().SaveCommand.ExecuteAsync(null);

        await _library.DidNotReceiveWithAnyArgs().AddClipAsync(default!, default!, default);
    }

    [Fact]
    public void Tone_EveryPresetFitsInsideTheManualControls()
    {
        // The page's sliders clamp whatever they are given, so a preset outside a range would quietly play a different sound
        // from the one it is named after.
        foreach (TonePreset preset in CreateTone().Presets)
        {
            bool isNoise = preset.Waveform is WaveformType.WhiteNoise or WaveformType.PinkNoise;
            if (!isNoise)
            {
                Assert.InRange(preset.FrequencyHz, ToneGeneratorViewModel.MinFrequencyHz, ToneGeneratorViewModel.MaxFrequencyHz);
            }

            Assert.InRange(preset.DurationSeconds, ToneGeneratorViewModel.MinDurationSeconds, ToneGeneratorViewModel.MaxDurationSeconds);
            Assert.InRange(preset.Amplitude, 0, 1);
        }
    }

    [Theory]
    [InlineData(WaveformType.Sine, true)]
    [InlineData(WaveformType.Square, true)]
    [InlineData(WaveformType.Triangle, true)]
    [InlineData(WaveformType.Sawtooth, true)]
    [InlineData(WaveformType.WhiteNoise, false)]
    [InlineData(WaveformType.PinkNoise, false)]
    public void Tone_FrequencyOnlyMattersForPitchedWaveforms(WaveformType waveform, bool relevant)
    {
        ToneGeneratorViewModel viewModel = CreateTone();
        List<string?> raised = [];
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        viewModel.SelectedWaveform = waveform;

        Assert.Equal(relevant, viewModel.IsFrequencyRelevant);
        if (waveform != WaveformType.Sine)
        {
            Assert.Contains(nameof(ToneGeneratorViewModel.IsFrequencyRelevant), raised);
        }
    }

    [Theory]
    [InlineData(WaveformType.WhiteNoise, "WhiteNoise")]
    [InlineData(WaveformType.PinkNoise, "PinkNoise")]
    public async Task Tone_Generate_NoiseIsNamedWithoutAFrequency(WaveformType noise, string expectedName)
    {
        AudioClip preview = PreviewClip();
        _synthesis.GenerateToneAsync(default, default, default, default, default!).ReturnsForAnyArgs(preview);
        ToneGeneratorViewModel viewModel = CreateTone();
        viewModel.SelectedWaveform = noise;

        await viewModel.GenerateCommand.ExecuteAsync(null);

        await _synthesis.Received(1).GenerateToneAsync(noise, Arg.Any<double>(), Arg.Any<double>(), Arg.Any<double>(), expectedName);
    }

    [Fact]
    public async Task Tone_Generate_SynthesisFails_ReportsItAndAllowsNoSave()
    {
        _synthesis.GenerateToneAsync(default, default, default, default, default!).ReturnsForAnyArgs(Task.FromException<AudioClip>(new IOException()));
        ToneGeneratorViewModel viewModel = CreateTone();

        await viewModel.GenerateCommand.ExecuteAsync(null);

        Assert.Equal("Couldn't generate that tone.", viewModel.StatusMessage);
        Assert.False(viewModel.CanSave);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task Tone_Generate_SynthesizesPreviewsAndEnablesSave()
    {
        AudioClip clip = PreviewClip("Sine 440Hz");
        _synthesis.GenerateToneAsync(WaveformType.Sine, 440, 1.0, 0.8, "Sine 440Hz").Returns(clip);
        ToneGeneratorViewModel viewModel = CreateTone();

        await viewModel.GenerateCommand.ExecuteAsync(null);

        Assert.True(viewModel.CanSave);
        Assert.Equal([0.3f, 0.6f], viewModel.WaveformPeaks);
        Assert.Equal("Previewing - tap Save to keep it in your library.", viewModel.StatusMessage);
        Assert.False(viewModel.IsBusy);
        Received.InOrder(
        () =>
        {
            _playback.LoadAsync(clip);
            _playback.Play();
        });
    }

    [Fact]
    public async Task Tone_Generate_UsesTheChosenSettings()
    {
        AudioClip clip = PreviewClip();
        _synthesis.GenerateToneAsync(WaveformType.Square, 1250, 2.5, 0.4, "Square 1250Hz").Returns(clip);
        ToneGeneratorViewModel viewModel = CreateTone();
        viewModel.SelectedWaveform = WaveformType.Square;
        viewModel.FrequencyHz = 1250;
        viewModel.DurationSeconds = 2.5;
        viewModel.Amplitude = 0.4;

        await viewModel.GenerateCommand.ExecuteAsync(null);

        await _synthesis.Received(1).GenerateToneAsync(WaveformType.Square, 1250, 2.5, 0.4, "Square 1250Hz");
    }

    [Fact]
    public async Task Tone_Generate_WhileBusy_DoesNothing()
    {
        ToneGeneratorViewModel viewModel = CreateTone();
        viewModel.IsBusy = true;

        await viewModel.GenerateCommand.ExecuteAsync(null);

        await _synthesis.DidNotReceiveWithAnyArgs().GenerateToneAsync(default, default, default, default, default!);
    }

    [Fact]
    public void Tone_OffersEveryWaveform() { Assert.Equal(Enum.GetValues<WaveformType>(), CreateTone().WaveformOptions); }
    [Fact]
    public async Task Tone_Save_AddsThePreviewToTheLibraryOnce()
    {
        AudioClip clip = PreviewClip("Sine 440Hz");
        _synthesis.GenerateToneAsync(default, default, default, default, default!).ReturnsForAnyArgs(clip);
        _library.AddClipAsync(clip.FilePath, "Sine 440Hz", true).Returns(TestData.Clip("Sine 440Hz"));
        ToneGeneratorViewModel viewModel = CreateTone();
        await viewModel.GenerateCommand.ExecuteAsync(null);

        await viewModel.SaveCommand.ExecuteAsync(null);
        await viewModel.SaveCommand.ExecuteAsync(null);

        await _library.Received(1).AddClipAsync(clip.FilePath, "Sine 440Hz", true);
        Assert.False(viewModel.CanSave);
        Assert.Equal("Saved 'Sine 440Hz' to your library.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Tone_Save_ExportedToSharedStorage_NamesTheLocation()
    {
        AudioClip preview = PreviewClip("Tone");
        _synthesis.GenerateToneAsync(default, default, default, default, default!).ReturnsForAnyArgs(preview);
        _library.AddClipAsync(default!, default!, default).ReturnsForAnyArgs(TestData.Clip("Tone", publicLocation: "Music/Tone.wav"));
        ToneGeneratorViewModel viewModel = CreateTone();
        await viewModel.GenerateCommand.ExecuteAsync(null);

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Saved 'Tone' - also in Music/Tone.wav.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task Tone_Save_NothingGeneratedYet_DoesNothing()
    {
        await CreateTone().SaveCommand.ExecuteAsync(null);

        await _library.DidNotReceiveWithAnyArgs().AddClipAsync(default!, default!, default);
    }

    [Fact]
    public void Tone_SelectPreset_LoadsItsSettingsIntoTheControls()
    {
        ToneGeneratorViewModel viewModel = CreateTone();
        TonePreset preset = TonePreset.All.First(p => p.Name == "Alarm Beep");

        viewModel.SelectPresetCommand.Execute(preset);

        Assert.Equal(WaveformType.Square, viewModel.SelectedWaveform);
        Assert.Equal(880, viewModel.FrequencyHz);
        Assert.Equal(0.3, viewModel.DurationSeconds);
        Assert.Equal(0.9, viewModel.Amplitude);
    }

    [Fact]
    public void Tone_SelectPreset_NoisePresetLeavesTheFrequencyAlone()
    {
        ToneGeneratorViewModel viewModel = CreateTone();
        viewModel.FrequencyHz = 1234;
        TonePreset hiss = TonePreset.All.First(p => p.Waveform == WaveformType.WhiteNoise);

        viewModel.SelectPresetCommand.Execute(hiss);

        Assert.Equal(WaveformType.WhiteNoise, viewModel.SelectedWaveform);
        Assert.Equal(1234, viewModel.FrequencyHz);
    }

    [Fact]
    public void Tone_StartsAsAnEightyPercentSineAtConcertPitch()
    {
        ToneGeneratorViewModel viewModel = CreateTone();

        Assert.Equal("Tone Generator", viewModel.Title);
        Assert.Equal(WaveformType.Sine, viewModel.SelectedWaveform);
        Assert.Equal(440, viewModel.FrequencyHz);
        Assert.Equal(1.0, viewModel.DurationSeconds);
        Assert.Equal(0.8, viewModel.Amplitude);
        Assert.False(viewModel.CanSave);
    }
    #endregion
}
