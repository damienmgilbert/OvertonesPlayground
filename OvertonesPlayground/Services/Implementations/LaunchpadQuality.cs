using OvertonesPlayground.Models;
using OvertonesPlayground.Ontology.Querying;
using OvertonesPlayground.Ontology.Styles;
using OvertonesPlayground.Ontology.Theory;
using static OvertonesPlayground.Services.Implementations.LaunchpadExampleBuilder;

namespace OvertonesPlayground.Services.Implementations;

///<summary>
///The checks a generated setup must pass before anyone hears it, the same rules the hand-written examples are held to:
///<list type="bullet">
///<item>the kit bank has kicks, a backbeat and hats, and all four sequencer tracks have a sound;</item>
///<item>every sound is in the bank, and no bank holds the same sound twice;</item>
///<item>nothing clashes with the key, and every loop is at the tempo;</item>
///<item>kicks, snares and bass notes sit solidly in the centre, and kicks leave the sub-bass to a bass;</item>
///<item>the tempo and swing are the style's, and the groove's hats are as busy as the style's hats are.</item>
///</list>
///</summary>
internal static class LaunchpadQuality
{
    #region Constants
    private const int KitBank = 0;
    private const double DensityTolerance = 0.05;
    #endregion

    #region Public methods
    ///<summary>
    ///What is wrong with <paramref name="recipe"/>, in words; empty when it passes.
    ///</summary>
    public static IReadOnlyList<string> Check(LaunchpadRecipe recipe, SampleIndex index)
    {
        List<string> problems = [];
        StyleProfile? profile = StyleProfiles.Find(recipe.StyleKey);
        Key key = recipe.Key;

        foreach ((int lane, string role, int minimum) in new[] { (Low, "kicks", 3), (Backbeat, "backbeat sounds", 2), (Hats, "hats", 2) })
        {
            int count = recipe.SoundsAt(KitBank, lane).Count;
            if (count < minimum)
            {
                problems.Add($"bank A has {count} {role}, fewer than {minimum}");
            }
        }

        if (recipe.Tracks.Select(track => track.Track).Distinct().Count() < LaunchpadPattern.TrackCount)
        {
            problems.Add("a sequencer track has no sound");
        }

        foreach (IGrouping<int, string> bank in recipe.Fills.SelectMany(fill => fill.Sounds.Select(sound => (fill.Bank, sound.SampleId))).GroupBy(pair => pair.Bank, pair => pair.SampleId))
        {
            string? twice = bank.GroupBy(id => id, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1)?.Key;
            if (twice is not null)
            {
                problems.Add($"'{twice}' is twice in bank {(char)('A' + bank.Key)}");
            }
        }

        bool hasBass = recipe.Fills.Any(fill => fill.Bank == KitBank && fill.Lane == Colour && fill.Sounds.Any(sound => sound.Level == ExampleLevel.Bass));
        foreach (RecipeFill fill in recipe.Fills)
        {
            foreach ((string id, ExampleLevel level) in fill.Sounds)
            {
                if (index.Find(id) is not { } sample)
                {
                    problems.Add($"'{id}' is not in the sound bank");
                    continue;
                }

                if (!KeyCompatibility.CanPlayIn(sample, key))
                {
                    problems.Add($"'{sample.Name}' clashes with {key.Name}");
                }

                if (fill.Loop && !RunsAt(sample, recipe.Tempo))
                {
                    problems.Add($"'{sample.Name}' is not at {recipe.Tempo} BPM");
                }

                bool isCentre = fill.Bank == KitBank && level is ExampleLevel.Kick or ExampleLevel.Snare;
                if (isCentre && !IsSolidCentre(sample))
                {
                    problems.Add($"'{sample.Name}' is too wide for the centre of the mix");
                }

                if (hasBass && fill.Bank == KitBank && level == ExampleLevel.Kick && Sub(sample) > MaxKickSubWithBass)
                {
                    problems.Add($"the kick '{sample.Name}' would crowd the bass");
                }
            }
        }

        if (profile is not null)
        {
            if (!profile.Tempo.Contains(recipe.Tempo))
            {
                problems.Add($"{recipe.Tempo} BPM is outside {profile.Name}'s {profile.Tempo.Min}-{profile.Tempo.Max}");
            }

            if (!profile.Swing.Contains(recipe.SwingLevel))
            {
                problems.Add($"swing {recipe.SwingLevel} is outside {profile.Name}'s range");
            }

            RecipePattern? groove = recipe.Patterns.FirstOrDefault(pattern => pattern.Index == 0);
            string? hats = groove?.Lines.FirstOrDefault(line => line.Track == 2).Steps;
            if (hats is not null)
            {
                double density = StepPattern.Parse(hats).Density;
                if (density < profile.MinHatDensity - DensityTolerance || density > profile.MaxHatDensity + DensityTolerance)
                {
                    problems.Add($"the hats play {density:P0} of the steps, outside {profile.Name}'s {profile.MinHatDensity:P0}-{profile.MaxHatDensity:P0}");
                }
            }
        }

        return problems;
    }
    #endregion
}
