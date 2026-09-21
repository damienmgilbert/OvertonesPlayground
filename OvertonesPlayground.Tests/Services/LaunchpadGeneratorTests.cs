using System.Text.Json;
using OvertonesPlayground.Ontology.Theory;
using OvertonesPlayground.Services.Implementations;

namespace OvertonesPlayground.Tests.Services;

///<summary>
///Holds the project generator to its rules against the real sound bank, for every style and many seeds: every setup it makes
///passes the quality checks, keeps to its key and tempo, and comes out the same every time for the same request.
///</summary>
public sealed class LaunchpadGeneratorTests
{
    #region Constants
    private const int SeedsPerStyle = 12;
    #endregion

    #region Private methods
    private static LaunchpadRecipe Generate(string style, int seed, int? tempo = null, string? key = null, string? kit = null) =>
        LaunchpadGenerator.Generate(new LaunchpadGenerationRequest(style, seed, tempo, key, kit), RealCatalog.Index);

    public static TheoryData<string> Styles() => [.. StyleProfiles.All.Select(profile => profile.Key)];

    private static IEnumerable<Sample> SamplesOf(LaunchpadRecipe recipe) =>
        recipe.Fills.SelectMany(fill => fill.Sounds).Select(sound => RealCatalog.Index.Find(sound.SampleId)!);
    #endregion

    #region Public methods
    [Theory]
    [MemberData(nameof(Styles))]
    public void Generate_ManySeeds_EverySetupPassesTheQualityChecks(string style)
    {
        for (int seed = 1; seed <= SeedsPerStyle; seed++)
        {
            LaunchpadRecipe recipe = Generate(style, seed);

            Assert.Empty(LaunchpadQuality.Check(recipe, RealCatalog.Index));
        }
    }

    [Theory]
    [MemberData(nameof(Styles))]
    public void Generate_ManySeeds_NothingClashesWithTheKeyAndEveryLoopIsAtTheTempo(string style)
    {
        for (int seed = 100; seed < 100 + SeedsPerStyle; seed++)
        {
            LaunchpadRecipe recipe = Generate(style, seed);
            Key key = recipe.Key;

            Assert.All(SamplesOf(recipe), sample => Assert.NotEqual(KeyFit.Clashes, KeyCompatibility.Judge(sample, key)));
            Assert.All(recipe.Fills.Where(fill => fill.Loop).SelectMany(fill => fill.Sounds), sound => Assert.True(LaunchpadExampleBuilder.RunsAt(RealCatalog.Index.Find(sound.SampleId)!, recipe.Tempo), sound.SampleId));
        }
    }

    [Theory]
    [MemberData(nameof(Styles))]
    public void Generate_TempoAndSwing_AreInTheStylesRange(string style)
    {
        StyleProfile profile = StyleProfiles.Get(style);
        for (int seed = 200; seed < 200 + SeedsPerStyle; seed++)
        {
            LaunchpadRecipe recipe = Generate(style, seed);

            Assert.InRange(recipe.Tempo, profile.Tempo.Min, profile.Tempo.Max);
            Assert.InRange(recipe.SwingLevel, profile.Swing.Min, profile.Swing.Max);
            Assert.Contains(LaunchpadScale.Of(recipe.ScaleIndex).Key, profile.Scales);
        }
    }

    [Theory]
    [MemberData(nameof(Styles))]
    public void Generate_SameRequest_GivesTheSameProject(string style)
    {
        LaunchpadProject first = Generate(style, 42).Build(RealCatalog.Index, sample => sample.Id);
        LaunchpadProject second = Generate(style, 42).Build(RealCatalog.Index, sample => sample.Id);

        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    [Fact]
    public void Generate_DifferentSeeds_GiveDifferentProjects()
    {
        HashSet<string> projects = [.. Enumerable.Range(1, 10).Select(seed => JsonSerializer.Serialize(Generate("house", seed).Build(RealCatalog.Index, sample => sample.Id)))];

        Assert.True(projects.Count >= 9, $"only {projects.Count} different projects from 10 seeds");
    }

    [Theory]
    [MemberData(nameof(Styles))]
    public void Build_Project_IsCompleteAndSurvivesNormalizeUnchanged(string style)
    {
        LaunchpadRecipe recipe = Generate(style, 7);
        LaunchpadProject project = recipe.Build(RealCatalog.Index, sample => sample.Id);
        string before = JsonSerializer.Serialize(project);

        project.Normalize();

        Assert.Equal(before, JsonSerializer.Serialize(project));
        Assert.Equal(recipe.RootPitchClass, project.RootPitchClass);
        Assert.Equal(style, project.StyleKey);
        Assert.Equal(recipe.Seed, project.Seed);
        Assert.Equal(LaunchpadProjectOrigin.Generated, project.Origin);
        Assert.All(project.Sequence.Tracks, track => Assert.True(track.HasSource));
        Assert.All(project.Pads, pad => Assert.NotNull(RealCatalog.Index.Find(pad.ClipPath!)));
        Assert.True(project.Sequence.Patterns.Take(4).All(pattern => pattern.Tracks.Any(track => track.Any(step => step.IsOn))));
    }

    [Fact]
    public void Generate_WithAKeyTempoAndKit_UsesThem()
    {
        LaunchpadRecipe recipe = Generate("boom-bap", 3, tempo: 90, key: "E minor", kit: "vinyl");

        Assert.Equal(90, recipe.Tempo);
        Assert.Equal("E minor", recipe.Key.Name);
        Assert.Equal("vinyl", recipe.Kit);
        Assert.Contains("90 BPM", recipe.Title, StringComparison.Ordinal);
        Assert.Contains("E minor", recipe.Title, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_AtATempoWithLoops_FillsTheLoopBank()
    {
        LaunchpadRecipe recipe = Generate("boom-bap", 5, tempo: 90, key: "E minor");

        Assert.Contains(recipe.Fills, fill => fill.Bank == 1 && fill.Loop);
    }

    [Fact]
    public void Generate_Trap_KeepsKicksClearOfThe808()
    {
        for (int seed = 1; seed <= 10; seed++)
        {
            LaunchpadRecipe recipe = Generate("trap", seed);
            IEnumerable<Sample> kicks = recipe.SoundsAt(0, LaunchpadExampleBuilder.Low).Select(id => RealCatalog.Index.Find(id)!);

            Assert.All(kicks, kick => Assert.True(LaunchpadExampleBuilder.Sub(kick) <= LaunchpadExampleBuilder.MaxKickSubWithBass, kick.Name));
        }
    }

    [Fact]
    public void Generate_UnknownStyle_Throws() =>
        Assert.Throws<KeyNotFoundException>(() => Generate("polka", 1));

    [Fact]
    public void Presets_EveryPresetBuildsAndPassesTheChecks()
    {
        Assert.NotEmpty(LaunchpadGenerator.Presets);
        Assert.All(LaunchpadGenerator.Presets, info =>
        {
            LaunchpadRecipe recipe = LaunchpadGenerator.Preset(info.Id, RealCatalog.Index);

            Assert.Empty(LaunchpadQuality.Check(recipe, RealCatalog.Index));
            Assert.Contains("BPM", info.Name, StringComparison.Ordinal);
            Assert.NotNull(info.StyleKey);
        });
    }

    [Fact]
    public void Presets_BuildWithTheirOwnSeedAndChoices()
    {
        Assert.All(StyleProfiles.Presets, pair =>
        {
            LaunchpadRecipe recipe = LaunchpadGenerator.Preset(pair.Preset.Id, RealCatalog.Index);

            Assert.Equal(pair.Preset.Seed, recipe.Seed);
            Assert.Equal(pair.Preset.Tempo, recipe.Tempo);
            Assert.Equal(pair.Preset.Kit, recipe.Kit);
            Assert.Equal(Key.Parse(pair.Preset.Key), recipe.Key);
        });
    }

    [Fact]
    public void Presets_HaveUniqueIdsThatDoNotCollideWithTheExamples()
    {
        List<string> ids = [.. LaunchpadGenerator.Presets.Select(p => p.Id).Concat(LaunchpadExamples.All.Select(e => e.Id))];

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Styles_ListsEveryProfile() =>
        Assert.Equal(StyleProfiles.All.Select(p => p.Key), LaunchpadGenerator.Styles.Select(s => s.Key));
    #endregion
}
