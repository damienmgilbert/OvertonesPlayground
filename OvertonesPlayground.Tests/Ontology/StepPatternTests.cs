using OvertonesPlayground.Ontology.Theory;

namespace OvertonesPlayground.Tests.Ontology;

///<summary>
///Step patterns: reading and writing the step notation, measuring a groove, and the variations the generator plays.
///</summary>
public sealed class StepPatternTests
{
    #region Fields
    private static readonly Meter _meter = Meter.Common;
    #endregion

    #region Public methods
    [Theory]
    [InlineData("X.....x...X.....")]
    [InlineData("X.o.x.o?X.o.x.o?")]
    [InlineData("....X..-.-..X..-")]
    public void Parse_ThenToString_GivesTheSameSteps(string steps) => Assert.Equal(steps, StepPattern.Parse(steps).ToString());

    [Fact]
    public void Parse_AStrangeCharacter_Throws() => Assert.Throws<FormatException>(() => StepPattern.Parse("X..Z"));

    [Fact]
    public void Density_IsTheShareOfStepsThatPlay()
    {
        Assert.Equal(0.25, StepPattern.Parse("X...X...X...X...").Density);
        Assert.Equal(0, StepPattern.Empty(16).Density);
    }

    [Fact]
    public void Syncopation_IsZeroOnTheBeatAndHighBetweenThem()
    {
        Assert.Equal(0, StepPattern.Parse("X...X...X...X...").Syncopation(_meter));
        Assert.True(StepPattern.Parse(".X.X.X.X.X.X.X.X").Syncopation(_meter) > 0.9);
    }

    [Fact]
    public void Meter_KnowsBeatsBackbeatsAndDownbeats()
    {
        Assert.True(_meter.IsBackbeat(4));
        Assert.True(_meter.IsBackbeat(12));
        Assert.False(_meter.IsBackbeat(8));
        Assert.True(_meter.IsDownbeat(16));
        Assert.True(_meter.IsOffEighth(3));
        Assert.Equal(16, _meter.StepsPerBar);
    }

    [Fact]
    public void Rotate_And_Fit_MoveAndRepeatTheSteps()
    {
        Assert.Equal(".X..", StepPattern.Parse("X...").Rotate(1).ToString());
        Assert.Equal("X.X.X.X.", StepPattern.Parse("X.").Fit(8).ToString());
    }

    [Fact]
    public void Thin_KeepsTheDownbeatAndLoudBeats()
    {
        StepPattern groove = StepPattern.Parse("X.x.X.x.X.x.X.x.");
        for (int seed = 0; seed < 20; seed++)
        {
            StepPattern thin = groove.Thin(new Random(seed), _meter, keep: 0);

            Assert.Equal("X...X...X...X...", thin.ToString());
        }
    }

    [Fact]
    public void AddGhosts_OnlyAddsQuietOffBeatHitsNextToExistingOnes()
    {
        StepPattern groove = StepPattern.Parse("X.......X.......");
        StepPattern ghosted = groove.AddGhosts(new Random(1), _meter, maxAdded: 4, chance: 1);

        Assert.True(ghosted.HitCount > groove.HitCount);
        for (int i = 0; i < 16; i++)
        {
            if (ghosted.Steps[i].IsOn && !groove.Steps[i].IsOn)
            {
                Assert.False(_meter.IsOnBeat(i));
                Assert.Equal(0.25, ghosted.Steps[i].Velocity);
            }
        }
    }

    [Fact]
    public void Loosen_NeverTouchesTheLoudHits()
    {
        StepPattern groove = StepPattern.Parse("X.o.X.o.X.o.X.o.");
        StepPattern loose = groove.Loosen(new Random(3), _meter, chance: 1);

        Assert.Equal("X.?.X.?.X.?.X.?.", loose.ToString());
    }

    [Fact]
    public void Fill_RampsUpTheLastBeat()
    {
        StepPattern fill = StepPattern.Parse("....X.......X...").Fill(_meter);

        Assert.Equal("....X.......", fill.ToString()[..12]);
        Assert.True(fill.Steps.Skip(12).All(step => step.IsOn));
        Assert.True(fill.Steps[15].Velocity > fill.Steps[12].Velocity);
    }

    [Fact]
    public void Nudge_KeepsTheHitCountAndTheBeats()
    {
        StepPattern groove = StepPattern.Parse("X..x..x...X..x..");
        for (int seed = 0; seed < 20; seed++)
        {
            StepPattern nudged = groove.Nudge(new Random(seed), _meter);

            Assert.Equal(groove.HitCount, nudged.HitCount);
            Assert.All(Enumerable.Range(0, 16).Where(_meter.IsOnBeat), i => Assert.Equal(groove.Steps[i], nudged.Steps[i]));
        }
    }
    #endregion
}
