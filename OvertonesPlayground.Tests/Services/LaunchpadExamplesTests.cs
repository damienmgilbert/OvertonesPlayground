using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Tests.Services;

///<summary>
///Holds every ready-made Launchpad setup to the rules that make its sounds complement each other, against the real sound bank: the
///recipes choose sounds by rule, so what has to be checked is the rules and not a fixed list of files.
///</summary>
public sealed class LaunchpadExamplesTests
{
    private const int KitBank = 0;
    private const int LoopBank = 1;
    private static readonly int[] _minorScale = [0, 2, 3, 5, 7, 8, 10];

    public static TheoryData<string> ExampleIds
    {
        get
        {
            TheoryData<string> data = new();
            foreach (LaunchpadExampleInfo example in LaunchpadExamples.All)
            {
                data.Add(example.Id);
            }

            return data;
        }
    }

    // The key of each setup: root pitch class and whether it is minor.
    private static (int Root, bool IsMinor) KeyOf(string id) => id switch
    {
        LaunchpadExamples.BoomBapId => (4, true),
        LaunchpadExamples.TrapId => (0, true),
        LaunchpadExamples.ClubId => (5, true),
        _ => throw new ArgumentException(id),
    };

    private static LaunchpadExampleBuilder Assemble(string id) => LaunchpadExamples.Assemble(id, RealCatalog.Index, sample => sample.Id);

    private static IEnumerable<ExamplePlacement> Where(LaunchpadExampleBuilder builder, int bank, int? lane = null) =>
        builder.Placements.Where(p => p.Bank == bank && (lane is null || p.Lane == lane));

    private static bool IsInstrument(Sample sample, string key) => Taxonomies.Instruments.Get(sample.Classification.Instrument.Value).IsA(key);

    #region The list
    [Fact]
    public void All_ListsEachSetupOnceWithANameAndADescription()
    {
        Assert.Equal(["boom-bap", "trap", "club"], LaunchpadExamples.All.Select(example => example.Id));
        Assert.All(LaunchpadExamples.All, example =>
        {
            Assert.False(string.IsNullOrWhiteSpace(example.Name));
            Assert.Contains("BPM", example.Name, StringComparison.Ordinal);
            Assert.True(example.Description.Length > 100, example.Id);
        });
    }

    [Fact]
    public void Build_AnUnknownSetup_Throws()
    {
        _ = Assert.Throws<ArgumentException>(() => LaunchpadExamples.Build("nope", RealCatalog.Index, sample => sample.Id));
    }

    [Theory]
    [MemberData(nameof(ExampleIds))]
    public void Build_IsTheSameEveryTime(string id)
    {
        string First() => System.Text.Json.JsonSerializer.Serialize(LaunchpadExamples.Build(id, RealCatalog.Index, sample => sample.Id));

        Assert.Equal(First(), First());
    }
    #endregion

    #region Pads
    [Theory]
    [MemberData(nameof(ExampleIds))]
    public void Pads_AreOnEachBankOnceAndPointAtRealSamples(string id)
    {
        LaunchpadProject project = LaunchpadExamples.Build(id, RealCatalog.Index, sample => sample.Id);

        Assert.NotEmpty(project.Pads);
        Assert.Equal(project.Pads.Count, project.Pads.Select(pad => (pad.Bank, pad.Index)).Distinct().Count());
        Assert.All(project.Pads, pad =>
        {
            Assert.InRange(pad.Bank, KitBank, LoopBank);
            Assert.InRange(pad.Index, 0, LaunchpadProject.PadsPerBank - 1);
            Sample sample = RealCatalog.Index.Find(pad.ClipPath!)!;
            Assert.NotNull(sample);
            Assert.Equal(pad.IsLooping ? LaunchpadExampleBuilder.WithoutTempo(sample.Name) : sample.Name, pad.Label);
            Assert.False(pad.IsLooping && pad.Label.Contains("bpm", StringComparison.OrdinalIgnoreCase), pad.Label);
            Assert.InRange(pad.Volume, 0.05, 1.0);
            Assert.Equal(LaunchpadColumn.Colors[pad.Index % LaunchpadProject.ColumnCount], pad.ColorHex);
        });
    }

    [Theory]
    [MemberData(nameof(ExampleIds))]
    public void Pads_TheKitBankIsOneShotsAndTheLoopBankIsLoops(string id)
    {
        LaunchpadProject project = LaunchpadExamples.Build(id, RealCatalog.Index, sample => sample.Id);

        Assert.All(project.Pads.Where(pad => pad.Bank == KitBank), pad => Assert.False(pad.IsLooping));
        Assert.All(project.Pads.Where(pad => pad.Bank == LoopBank), pad => Assert.True(pad.IsLooping));
    }

    [Theory]
    [MemberData(nameof(ExampleIds))]
    public void Lanes_KickHatAndTomColumnsEachHoldOneKindOfSound(string id)
    {
        LaunchpadExampleBuilder builder = Assemble(id);

        Assert.All(Where(builder, KitBank, LaunchpadExampleBuilder.Low), p => Assert.True(IsInstrument(p.Sample, "kick"), p.Sample.Name));
        Assert.All(Where(builder, KitBank, LaunchpadExampleBuilder.Hats), p => Assert.True(IsInstrument(p.Sample, "hihat"), p.Sample.Name));
        Assert.All(Where(builder, KitBank, LaunchpadExampleBuilder.Toms), p => Assert.True(IsInstrument(p.Sample, "tom"), p.Sample.Name));
    }

    [Theory]
    [MemberData(nameof(ExampleIds))]
    public void Lanes_ClosedAndOpenHatsShareAColumnWithBothWithinReachOfTheSequencer(string id)
    {
        // Radio only lets a pad cut the others in its own column, so a closed hat can only choke an open one if they share one.
        LaunchpadExampleBuilder builder = Assemble(id);
        List<Sample> reachable = [.. Where(builder, KitBank, LaunchpadExampleBuilder.Hats).Where(p => p.Row >= LaunchpadExampleBuilder.FirstSelectableRow).Select(p => p.Sample)];

        Assert.Contains(reachable, sample => sample.Classification.Instrument.Value == "hihat-closed");
        Assert.Contains(reachable, sample => sample.Classification.Instrument.Value == "hihat-open");
    }

    [Theory]
    [MemberData(nameof(ExampleIds))]
    public void Lanes_TheMostTypicalSoundOfEachLaneIsOnTheBottomRow(string id)
    {
        foreach (IGrouping<(int, int), ExamplePlacement> lane in Assemble(id).Placements.GroupBy(p => (p.Bank, p.Lane)))
        {
            Assert.Contains(lane, p => p.Row == LaunchpadExampleBuilder.PrimaryRow);
            Assert.Equal(lane.Count(), lane.Select(p => p.Row).Distinct().Count());
            Assert.Equal(Enumerable.Range(0, lane.Count()).Select(i => LaunchpadExampleBuilder.PrimaryRow - i), lane.OrderBy(p => -p.Row).Select(p => p.Row));
        }
    }
    #endregion

    #region Complementing sounds
    [Theory]
    [MemberData(nameof(ExampleIds))]
    public void Drums_ComeFromAtMostTwoKits(string id)
    {
        IEnumerable<string> kits = Where(Assemble(id), KitBank)
            .Where(p => p.Lane != LaunchpadExampleBuilder.Colour)
            .Select(p => p.Sample.Classification.Kit?.Value)
            .OfType<string>()
            .Distinct();

        Assert.InRange(kits.Count(), 1, 2);
    }

    [Theory]
    [MemberData(nameof(ExampleIds))]
    public void CentreOfTheMix_KicksSnaresAndHatsSurviveBeingSummedToMono(string id)
    {
        LaunchpadExampleBuilder builder = Assemble(id);
        int[] centreLanes = [LaunchpadExampleBuilder.Low, LaunchpadExampleBuilder.Backbeat, LaunchpadExampleBuilder.Hats];

        Assert.All(Where(builder, KitBank).Where(p => centreLanes.Contains(p.Lane)), p =>
        {
            Assert.True(LaunchpadExampleBuilder.IsSolidCentre(p.Sample), $"{p.Sample.Name} is {p.Sample.Stereo!.Image} ({p.Sample.Stereo.MonoCompatibilityDb:0.0} dB when summed to mono)");
        });
    }

    [Theory]
    [InlineData(LaunchpadExamples.BoomBapId)]
    [InlineData(LaunchpadExamples.TrapId)]
    public void Kicks_LeaveRoomForThe808Bass(string id)
    {
        LaunchpadExampleBuilder builder = Assemble(id);

        Assert.Contains(Where(builder, KitBank, LaunchpadExampleBuilder.Colour), p => p.Level == ExampleLevel.Bass);
        Assert.All(Where(builder, KitBank, LaunchpadExampleBuilder.Low), p => Assert.True(p.Sample.Spectral!.Bands.Sub <= LaunchpadExampleBuilder.MaxKickSubWithBass, $"{p.Sample.Name} has {p.Sample.Spectral!.Bands.Sub:P0} of its energy below 60 Hz"));
    }

    [Fact]
    public void Trap_TheKickIsNotOneOfThe808KitsLongSubBassTails()
    {
        LaunchpadExampleBuilder builder = Assemble(LaunchpadExamples.TrapId);

        Assert.All(Where(builder, KitBank, LaunchpadExampleBuilder.Low), p =>
        {
            Assert.NotEqual("808", p.Sample.Classification.Kit?.Value);
            Assert.True(p.Sample.Technical!.DurationSeconds <= 0.4);
        });
        Assert.All(Where(builder, KitBank, LaunchpadExampleBuilder.Backbeat), p => Assert.Equal("808", p.Sample.Classification.Kit?.Value));
    }

    [Fact]
    public void Setups_DrawOnDifferentKitsSoTheyDoNotSoundAlike()
    {
        string?[] kickKits = [.. LaunchpadExamples.All.Select(example => Where(Assemble(example.Id), KitBank, LaunchpadExampleBuilder.Low).First().Sample.Classification.Kit?.Value)];
        string?[] hatKits = [.. LaunchpadExamples.All.Select(example => Where(Assemble(example.Id), KitBank, LaunchpadExampleBuilder.Hats).First().Sample.Classification.Kit?.Value)];

        Assert.Equal(kickKits.Length, kickKits.Distinct().Count());
        Assert.Equal(hatKits.Length, hatKits.Distinct().Count());
    }

    [Fact]
    public void Club_TheNineOhNineIsCompletedByASecondKitThatHasHatsAndAClap()
    {
        LaunchpadExampleBuilder builder = Assemble(LaunchpadExamples.ClubId);

        Assert.All(Where(builder, KitBank, LaunchpadExampleBuilder.Low), p => Assert.Equal("909", p.Sample.Classification.Kit?.Value));
        Assert.All(Where(builder, KitBank, LaunchpadExampleBuilder.Backbeat), p => Assert.Equal("909", p.Sample.Classification.Kit?.Value));
        Assert.All(Where(builder, KitBank, LaunchpadExampleBuilder.Hats), p => Assert.NotEqual("909", p.Sample.Classification.Kit?.Value));
        Assert.Contains(Where(builder, KitBank, LaunchpadExampleBuilder.Accent), p => p.Level == ExampleLevel.Clap && p.Sample.Classification.Kit?.Value != "909");
    }

    [Theory]
    [MemberData(nameof(ExampleIds))]
    public void Levels_EveryPadIsBroughtToItsRoleAndNeverLouderThanIt(string id)
    {
        LaunchpadExampleBuilder builder = Assemble(id);
        foreach (ExamplePlacement placement in builder.Placements)
        {
            double lufs = placement.Sample.Dynamics!.IntegratedLufs;
            double target = LaunchpadExampleBuilder.TargetLufs(placement.Level);
            double heard = lufs + (20 * Math.Log10(placement.Gain));

            Assert.InRange(placement.Gain, 0.05, 1.0);
            Assert.True(heard <= target + 1e-6, $"{placement.Sample.Name} would be {heard:0.0} LUFS, above its {target:0} target");
            bool isFullVolume = placement.Gain >= 1.0 || placement.Gain <= 0.05;
            if (!isFullVolume)
            {
                Assert.InRange(heard, target - 0.1, target);
            }
        }

        Assert.All(builder.Project.Pads, pad => Assert.Contains(builder.Placements, p => p.Sample.Id == pad.ClipPath && Math.Abs(p.Gain - pad.Volume) < 1e-9));
    }
    #endregion

    #region Tempo and key
    [Theory]
    [MemberData(nameof(ExampleIds))]
    public void Loops_RunAtExactlyTheTempoOfTheSetup(string id)
    {
        LaunchpadExampleBuilder builder = Assemble(id);

        Assert.NotEmpty(Where(builder, LoopBank));
        Assert.All(Where(builder, LoopBank).Where(p => p.Level != ExampleLevel.Texture), p =>
            Assert.True(LaunchpadExampleBuilder.RunsAt(p.Sample, builder.Project.Tempo), $"{p.Sample.Name} is at {p.Sample.Classification.Attributes.TempoBpm} BPM, the setup at {builder.Project.Tempo}"));
    }

    [Theory]
    [MemberData(nameof(ExampleIds))]
    public void Loops_AreInTheKeyOfTheSetup(string id)
    {
        (int root, bool isMinor) = KeyOf(id);

        Assert.All(Assemble(id).Placements, p => Assert.True(LaunchpadExampleBuilder.FitsKey(p.Sample, root, isMinor), $"{p.Sample.Name} clashes with the key"));
    }

    [Theory]
    [InlineData(LaunchpadExamples.BoomBapId)]
    [InlineData(LaunchpadExamples.TrapId)]
    public void BassNotes_AreNotesOfTheScale(string id)
    {
        (int root, _) = KeyOf(id);
        List<ExamplePlacement> notes = [.. Where(Assemble(id), KitBank).Where(p => p.Level == ExampleLevel.Bass)];

        Assert.NotEmpty(notes);
        Assert.All(notes, p =>
        {
            int pitchClass = p.Sample.EffectivePitchClass!.Value;
            Assert.Contains((((pitchClass - root) % 12) + 12) % 12, _minorScale);
        });
        Assert.Equal(root, notes[0].Sample.EffectivePitchClass);
    }

    [Fact]
    public void FitsKey_KeepsOutALoopAtTheRightTempoButInAClashingKey()
    {
        SampleIndex index = RealCatalog.Index;
        Sample clashing = index.Find("Mood Atmos E 130 bpm.wav")!;
        Sample inKey = index.Find("Chugged Arp Fmin 130 bpm.wav")!;
        Sample flatSeventh = index.Find("Atmos Colluding D# 130 bpm.wav")!;
        Sample eMinor = index.Find("Grand Piano Dirty Stabs E Minor 90 bpm.wav")!;

        Assert.True(LaunchpadExampleBuilder.RunsAt(clashing, 130));
        Assert.False(LaunchpadExampleBuilder.FitsKey(clashing, rootPitchClass: 5, isMinor: true));
        Assert.True(LaunchpadExampleBuilder.FitsKey(inKey, 5, true));
        Assert.False(LaunchpadExampleBuilder.FitsKey(inKey, 5, false));
        Assert.True(LaunchpadExampleBuilder.FitsKey(flatSeventh, 5, true));
        Assert.True(LaunchpadExampleBuilder.FitsKey(eMinor, 4, true));
        Assert.False(LaunchpadExampleBuilder.FitsKey(eMinor, 0, true));
    }

    [Fact]
    public void FitsKey_ASoundWithNoKeyInItsNameHasNothingToClash()
    {
        Assert.True(LaunchpadExampleBuilder.FitsKey(TestSamples.Make("Kick 1"), 7, true));
    }

    [Theory]
    [InlineData(0, 3, KeyMode.Major, true)]
    [InlineData(0, 0, KeyMode.Minor, true)]
    [InlineData(0, 0, KeyMode.Major, false)]
    [InlineData(0, 4, KeyMode.None, false)]
    [InlineData(0, 7, KeyMode.None, true)]
    public void FitsKey_AMajorKeyMustBeTheRelativeMajorOfAMinorSetup(int root, int pitchClass, KeyMode mode, bool expected)
    {
        Sample sample = TestSamples.Make("Loop") with { Classification = TestSamples.Make("Loop").Classification with { Attributes = new NamedAttributes(null, pitchClass, mode, null, null, "loop") } };

        Assert.Equal(expected, LaunchpadExampleBuilder.FitsKey(sample, root, isMinor: true));
    }

    [Fact]
    public void Loop_AtAnotherTempoOrInAnotherKey_IsRefusedSoItCannotDriftOrClash()
    {
        LaunchpadExampleBuilder club = new(RealCatalog.Index, sample => sample.Id, tempo: 130, swingLevel: 0, scaleIndex: 1);

        InvalidOperationException clash = Assert.Throws<InvalidOperationException>(() => club.Loop("Mood Atmos E 130 bpm", 5, true));
        InvalidOperationException drift = Assert.Throws<InvalidOperationException>(() => club.Loop("Break Ghosts 90 bpm", 5, true));
        InvalidOperationException missing = Assert.Throws<InvalidOperationException>(() => club.Loop("No Such Loop", 5, true));

        Assert.Contains("key", clash.Message, StringComparison.Ordinal);
        Assert.Contains("130 BPM", drift.Message, StringComparison.Ordinal);
        Assert.Contains("No Such Loop", missing.Message, StringComparison.Ordinal);
        Assert.Equal("Lava Beat 130 bpm", club.Loop("Lava Beat 130 bpm", 5, true).Name);
    }

    [Theory]
    [InlineData(89.6, 90, true)]
    [InlineData(90.4, 90, true)]
    [InlineData(91, 90, false)]
    [InlineData(120, 130, false)]
    public void RunsAt_AllowsHalfAPercentSoALoopStaysInTimeOverABar(double loopBpm, double setupBpm, bool expected)
    {
        Sample loop = TestSamples.Make("Loop", tempoBpm: loopBpm);

        Assert.Equal(expected, LaunchpadExampleBuilder.RunsAt(loop, setupBpm));
    }
    #endregion

    #region Sequencer, mixer and settings
    [Theory]
    [MemberData(nameof(ExampleIds))]
    public void Sequencer_EveryTrackPlaysAPadFromTheBottomFourRowsThroughItsOwnColumn(string id)
    {
        LaunchpadExampleBuilder builder = Assemble(id);

        Assert.Equal(LaunchpadExampleBuilder.TrackCount, builder.Project.Sequence.Tracks.Count(track => track.HasSource));
        foreach (LaunchpadTrack track in builder.Project.Sequence.Tracks)
        {
            Assert.Contains(Where(builder, KitBank), p => p.Sample.Id == track.ClipPath && p.Lane == track.Column && p.Row >= LaunchpadExampleBuilder.FirstSelectableRow);
            Assert.Equal(LaunchpadColumn.Colors[track.Column], track.ColorHex);
        }
    }

    [Theory]
    [MemberData(nameof(ExampleIds))]
    public void Sequencer_ThreePatternsOfSixteenStepsEachGiveEveryTrackSomethingToPlay(string id)
    {
        LaunchpadSequence sequence = Assemble(id).Project.Sequence;

        for (int pattern = 0; pattern < 3; pattern++)
        {
            Assert.Equal(16, sequence.Patterns[pattern].Length);
            Assert.All(sequence.Patterns[pattern].Tracks, steps => Assert.Contains(steps, step => step.IsOn));
        }

        Assert.All(sequence.Patterns.Skip(3), pattern => Assert.All(pattern.Tracks, steps => Assert.DoesNotContain(steps, step => step.IsOn)));
    }

    [Theory]
    [MemberData(nameof(ExampleIds))]
    public void Sequencer_StepsAreNoLouderThanTheirTrackAndAreOnlyEverWhollyOrHalfLikely(string id)
    {
        LaunchpadExampleBuilder builder = Assemble(id);

        for (int track = 0; track < LaunchpadExampleBuilder.TrackCount; track++)
        {
            double gain = builder.Placements.First(p => p.Bank == KitBank && p.Sample.Id == builder.Project.Sequence.Tracks[track].ClipPath).Gain;
            Assert.All(builder.Project.Sequence.Patterns.SelectMany(pattern => pattern.Tracks[track]).Where(step => step.IsOn), step =>
            {
                Assert.InRange(step.Velocity, 0.001, gain + 1e-9);
                Assert.Contains(step.Probability, new[] { 50, 100 });
            });
        }
    }

    [Theory]
    [MemberData(nameof(ExampleIds))]
    public void Sequencer_UsesTheLaunchpadsWholeVocabularyOfAccentsGhostNotesAndRests(string id)
    {
        List<LaunchpadStep> steps = [.. Assemble(id).Project.Sequence.Patterns.SelectMany(pattern => pattern.Tracks).SelectMany(track => track).Where(step => step.IsOn)];

        Assert.Contains(steps, step => step.Probability == 50);
        Assert.True(steps.Select(step => step.Velocity).Distinct().Count() >= 3, "velocities should vary");
    }

    [Theory]
    [MemberData(nameof(ExampleIds))]
    public void Settings_RadioIsOnAndTheTempoSwingScaleAndVolumeAreInRange(string id)
    {
        LaunchpadProject project = Assemble(id).Project;

        Assert.True(project.IsRadioOn);
        Assert.InRange(project.Tempo, 40, 240);
        Assert.InRange(project.SwingLevel, 0, 7);
        Assert.Equal(1, project.ScaleIndex);
        Assert.InRange(project.MasterVolume, 0.5, 1.0);
        Assert.Empty(project.Name);
    }

    [Theory]
    [MemberData(nameof(ExampleIds))]
    public void Mixer_ALaneWithEchoNeverHoldsALoop(string id)
    {
        // The echo plays a second copy of the pad a beat later; on a loop that would be a delayed duplicate of the whole loop.
        LaunchpadProject project = Assemble(id).Project;
        int[] echoed = [.. Enumerable.Range(0, LaunchpadProject.ColumnCount).Where(lane => project.Columns[lane].Send > 0)];

        Assert.NotEmpty(echoed);
        Assert.DoesNotContain(project.Pads, pad => pad.IsLooping && echoed.Contains(pad.Index % LaunchpadProject.ColumnCount));
    }

    [Theory]
    [MemberData(nameof(ExampleIds))]
    public void Mixer_HatsAndPercussionArePannedAwayFromTheCentreAndTheLowEndStaysInIt(string id)
    {
        IReadOnlyList<LaunchpadColumn> columns = Assemble(id).Project.Columns;

        Assert.Equal(0, columns[LaunchpadExampleBuilder.Low].Pan);
        Assert.Equal(0, columns[LaunchpadExampleBuilder.Backbeat].Pan);
        Assert.Equal(0, columns[LaunchpadExampleBuilder.Colour].Pan);
        Assert.True(columns[LaunchpadExampleBuilder.Hats].Pan > 0);
        Assert.True(columns[LaunchpadExampleBuilder.Percussion].Pan < 0);
        Assert.All(columns, column =>
        {
            Assert.InRange(column.Pan, -1, 1);
            Assert.InRange(column.Send, 0, 1);
            Assert.Equal(3, column.SpeedLevel);
        });
        Assert.Equal(0, columns[LaunchpadExampleBuilder.Low].Send);
        Assert.Equal(0, columns[LaunchpadExampleBuilder.Hats].Send);
    }

    [Theory]
    [MemberData(nameof(ExampleIds))]
    public void Project_SurvivesBeingSavedAndOpenedAgain(string id)
    {
        LaunchpadProject project = LaunchpadExamples.Build(id, RealCatalog.Index, sample => sample.Id);

        LaunchpadProject reopened = System.Text.Json.JsonSerializer.Deserialize<LaunchpadProject>(System.Text.Json.JsonSerializer.Serialize(project))!;
        reopened.Normalize();

        Assert.Equal(project.Pads.Count, reopened.Pads.Count);
        Assert.True(reopened.IsRadioOn);
        Assert.Equal(project.Tempo, reopened.Tempo);
        Assert.Equal(project.Sequence.Tracks.Select(track => track.ClipPath), reopened.Sequence.Tracks.Select(track => track.ClipPath));
    }
    #endregion

    #region Labels
    [Theory]
    [InlineData("Lava Beat 130 bpm", "Lava Beat")]
    [InlineData("Outer Bongos 140bpm", "Outer Bongos")]
    [InlineData("Break Ghosts 90 BPM", "Break Ghosts")]
    [InlineData("Chugged Arp Fmin 130 bpm", "Chugged Arp Fmin")]
    [InlineData("Crackle Vinyl Pop", "Crackle Vinyl Pop")]
    [InlineData("808 Heavy E", "808 Heavy E")]
    [InlineData("Kick 909 DMX 1", "Kick 909 DMX 1")]
    public void WithoutTempo_DropsOnlyTheTempoAtTheEnd(string name, string expected)
    {
        Assert.Equal(expected, LaunchpadExampleBuilder.WithoutTempo(name));
    }

    #endregion

    #region Step notation
    [Fact]
    public void Pattern_ReadsTheStepNotation()
    {
        LaunchpadExampleBuilder builder = new(RealCatalog.Index, sample => sample.Id, 90, 0, 1);

        builder.Pattern(0, (0, "Xxo-?."));

        List<LaunchpadStep> steps = builder.Project.Sequence.Patterns[0].Tracks[0];
        Assert.Equal(6, builder.Project.Sequence.Patterns[0].Length);
        Assert.Equal([1.0, 0.75, 0.5, 0.25, 0.5], steps.Take(5).Select(step => step.Velocity));
        Assert.Equal([100, 100, 100, 100, 50], steps.Take(5).Select(step => step.Probability));
        Assert.False(steps[5].IsOn);
    }

    [Fact]
    public void Pattern_ACharacterItDoesNotKnow_IsAMistakeInTheRecipe()
    {
        LaunchpadExampleBuilder builder = new(RealCatalog.Index, sample => sample.Id, 90, 0, 1);

        _ = Assert.Throws<ArgumentException>(() => builder.Pattern(0, (0, "X.Z.")));
    }

    [Fact]
    public void Track_ASampleFromTheTopHalfIsRefusedBecauseTheSequencerCannotPickIt()
    {
        LaunchpadExampleBuilder builder = new(RealCatalog.Index, sample => sample.Id, 90, 0, 1);
        builder.Fill(KitBank, LaunchpadExampleBuilder.Low, ExampleLevel.Kick, Enumerable.Range(0, 8).Select(i => TestSamples.Make($"Kick {i}")));

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => builder.Track(0, KitBank, LaunchpadExampleBuilder.Low, row: 0));
    }

    [Fact]
    public void Fill_MoreThanEightSoundsInALane_IsAMistakeInTheRecipe()
    {
        LaunchpadExampleBuilder builder = new(RealCatalog.Index, sample => sample.Id, 90, 0, 1);

        _ = Assert.Throws<InvalidOperationException>(() => builder.Fill(KitBank, 0, ExampleLevel.Kick, Enumerable.Range(0, 9).Select(i => TestSamples.Make($"Kick {i}"))));
    }
    #endregion

    #region Selection helpers
    [Fact]
    public void Spread_PicksSoundsAcrossTheWholeRangeInsteadOfTheFirstFew()
    {
        List<Sample> samples = [.. Enumerable.Range(0, 20).Select(i => TestSamples.Make($"Kick {i:00}", centroidHz: 50 + (i * 10)))];

        IReadOnlyList<Sample> picked = LaunchpadExampleBuilder.Spread(samples, 5, LaunchpadExampleBuilder.Centroid);

        Assert.Equal(5, picked.Count);
        Assert.Equal(50, picked[0].Spectral!.CentroidHz);
        Assert.Equal(240, picked[^1].Spectral!.CentroidHz);
    }

    [Fact]
    public void Spread_FewerCandidatesThanWanted_ReturnsThemAll()
    {
        Assert.Equal(2, LaunchpadExampleBuilder.Spread([TestSamples.Make("A"), TestSamples.Make("B")], 8, LaunchpadExampleBuilder.Centroid).Count);
    }

    [Fact]
    public void TypicalFirst_PutsTheSoundNearestTheMedianOnTheBottomRow()
    {
        List<Sample> samples = [.. new[] { 50, 60, 70, 300, 900 }.Select(hz => TestSamples.Make($"K{hz}", centroidHz: hz))];

        IReadOnlyList<Sample> ordered = LaunchpadExampleBuilder.TypicalFirst(samples, LaunchpadExampleBuilder.Centroid);

        Assert.Equal("K70", ordered[0].Name);
        Assert.Equal(5, ordered.Count);
    }

    [Fact]
    public void Interleave_AlternatesPairsOfClosedAndOpenHats()
    {
        Sample[] closed = [.. Enumerable.Range(0, 4).Select(i => TestSamples.Make($"C{i}"))];
        Sample[] open = [.. Enumerable.Range(0, 4).Select(i => TestSamples.Make($"O{i}"))];

        IReadOnlyList<Sample> lane = LaunchpadExampleBuilder.Interleave(closed, open);

        Assert.Equal(["C0", "C1", "O0", "O1", "C2", "C3", "O2", "O3"], lane.Select(sample => sample.Name));
    }

    [Fact]
    public void Interleave_WithFewOpenHats_StillFillsTheLane()
    {
        Sample[] closed = [.. Enumerable.Range(0, 6).Select(i => TestSamples.Make($"C{i}"))];

        IReadOnlyList<Sample> lane = LaunchpadExampleBuilder.Interleave(closed, [TestSamples.Make("O0")]);

        Assert.Equal(["C0", "C1", "O0", "C2", "C3", "C4", "C5"], lane.Select(sample => sample.Name));
    }

    [Fact]
    public void IsSolidCentre_RejectsWideOutOfPhaseAndUnclassifiedSounds()
    {
        Assert.True(LaunchpadExampleBuilder.IsSolidCentre(TestSamples.Make("Mono", image: StereoImage.Mono)));
        Assert.True(LaunchpadExampleBuilder.IsSolidCentre(TestSamples.Make("Narrow", image: StereoImage.Narrow)));
        Assert.False(LaunchpadExampleBuilder.IsSolidCentre(TestSamples.Make("Wide", image: StereoImage.Wide)));
        Assert.False(LaunchpadExampleBuilder.IsSolidCentre(TestSamples.Make("Phase", image: StereoImage.OutOfPhase)));
        Assert.False(LaunchpadExampleBuilder.IsSolidCentre(TestSamples.Make("Unsure", instrumentConfidence: 0.5)));
        Assert.False(LaunchpadExampleBuilder.IsSolidCentre(TestSamples.Make("Flagged", needsReview: true)));
    }

    [Theory]
    [InlineData(-15, -15, 1.0)]
    [InlineData(-10, -15, 0.562)]
    [InlineData(-40, -15, 1.0)]
    public void Gain_OnlyEverMakesASoundQuieterAndNeverSilent(double lufs, double target, double expected)
    {
        Sample sample = TestSamples.Make("Kick", lufs: lufs);

        Assert.Equal(expected, LaunchpadExampleBuilder.Gain(sample, ExampleLevel.Kick));
        Assert.Equal(target, LaunchpadExampleBuilder.TargetLufs(ExampleLevel.Kick));
    }
    #endregion
}
