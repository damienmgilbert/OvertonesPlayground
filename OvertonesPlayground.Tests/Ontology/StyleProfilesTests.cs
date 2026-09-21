using OvertonesPlayground.Ontology.Theory;

namespace OvertonesPlayground.Tests.Ontology;

///<summary>
///The style profiles in <c>Data/styles.json</c>: they load, every part of them is valid, and they describe the styles the way
///musicians would.
///</summary>
public sealed class StyleProfilesTests
{
    #region Public methods
    [Fact]
    public void All_LoadsFifteenStylesWithUniqueKeys()
    {
        Assert.Equal(15, StyleProfiles.All.Count);
        Assert.Equal(StyleProfiles.All.Count, StyleProfiles.All.Select(profile => profile.Key).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void All_EveryStyleIsAStyleOfTheTaxonomyAndHasPatternsScalesAndKits()
    {
        Assert.All(StyleProfiles.All, profile =>
        {
            Assert.True(Taxonomies.Styles.TryGet(profile.Style, out _), profile.Key);
            Assert.NotEmpty(profile.ScaleList);
            Assert.NotEmpty(profile.Kits);
            Assert.NotEmpty(profile.Kick);
            Assert.NotEmpty(profile.Backbeat);
            Assert.NotEmpty(profile.Hats);
            Assert.True(profile.Tempo.Min <= profile.Tempo.Max, profile.Key);
            Assert.InRange(profile.Swing.Min, 0, 7);
            Assert.InRange(profile.Swing.Max, profile.Swing.Min, 7);
            Assert.Contains(profile.BackbeatSound, new[] { "snare", "clap", "rim" });
        });
    }

    [Fact]
    public void All_EveryPatternIsOneBarOfSixteenthNotes()
    {
        IEnumerable<string> patterns = StyleProfiles.All.SelectMany(profile => profile.Kick.Concat(profile.Backbeat).Concat(profile.Hats).Concat(profile.ExtraPatterns));

        Assert.All(patterns, pattern => Assert.Equal(Meter.Common.StepsPerBar, StepPattern.Parse(pattern).Length));
    }

    [Fact]
    public void All_EveryKitIsAKitOfTheTaxonomy() =>
        Assert.All(StyleProfiles.All.SelectMany(profile => profile.Kits), kit => Assert.True(Taxonomies.Kits.TryGet(kit, out _), kit));

    [Fact]
    public void All_EveryStyleHasAProgressionThatGoesHome() =>
        Assert.All(StyleProfiles.All, profile => Assert.Contains(profile.ProgressionList, progression => progression.Numerals.Any(numeral => numeral is { Degree: 0, Accidental: 0 })));

    [Fact]
    public void FourOnTheFloorStyles_PutAKickOnEveryBeat()
    {
        foreach (string key in new[] { "house", "techno", "disco" })
        {
            StepPattern kick = StepPattern.Parse(StyleProfiles.Get(key).Kick[0]);

            Assert.All(new[] { 0, 4, 8, 12 }, beat => Assert.True(kick.Steps[beat].IsOn, key));
        }
    }

    [Fact]
    public void BackbeatStyles_HitTwoAndFour()
    {
        foreach (string key in new[] { "boom-bap", "house", "rock", "funk", "dnb" })
        {
            StepPattern backbeat = StepPattern.Parse(StyleProfiles.Get(key).Backbeat[0]);

            Assert.True(backbeat.Steps[4].IsOn && backbeat.Steps[12].IsOn, key);
        }
    }

    [Fact]
    public void Presets_HaveUniqueIdsAndKeysThatParse()
    {
        List<StylePreset> presets = [.. StyleProfiles.Presets.Select(pair => pair.Preset)];

        Assert.True(presets.Count >= 30);
        Assert.Equal(presets.Count, presets.Select(preset => preset.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(StyleProfiles.Presets, pair =>
        {
            Assert.True(Key.TryParse(pair.Preset.Key, out _), pair.Preset.Key);
            Assert.True(pair.Profile.Tempo.Contains(pair.Preset.Tempo), pair.Preset.Id);
        });
    }

    [Fact]
    public void Get_UnknownStyle_Throws() => Assert.Throws<KeyNotFoundException>(() => StyleProfiles.Get("polka"));
    #endregion
}
