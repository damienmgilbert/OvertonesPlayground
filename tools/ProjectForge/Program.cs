using System.Text.Json;
using OvertonesPlayground.Models;
using OvertonesPlayground.Ontology.Catalog;
using OvertonesPlayground.Ontology.Concepts;
using OvertonesPlayground.Ontology.Querying;
using OvertonesPlayground.Ontology.Styles;
using OvertonesPlayground.Services.Implementations;

// ProjectForge: proposes ready-made Launchpad presets for every style.
//
//   dotnet run --project tools/ProjectForge -- [--catalog <sample-catalog.json>] [--seeds <n>] [--per-style <n>] [--out <presets.json>]
//
// For each style it tries every kit the style lists, a spread of the tempos in its range and <seeds> seeds per kit, keeps the
// setups that pass LaunchpadQuality, scores them by how much they give to play (loops, harmony, full lanes, variety of
// instruments), and picks the best <per-style> with different kits or keys. The result is a JSON object of preset lists per style
// key, ready to paste into the "presets" of OvertonesPlayground.Ontology/Data/styles.json.

string catalogPath = Path.Combine(FindRepoRoot(), "OvertonesPlayground", "Resources", "Raw", "sample-catalog.json");
int seeds = 6;
int perStyle = 4;
string? outPath = null;
for (int i = 0; i < args.Length - 1; i++)
{
    switch (args[i])
    {
        case "--catalog":
            catalogPath = args[++i];
            break;
        case "--seeds":
            seeds = int.Parse(args[++i]);
            break;
        case "--per-style":
            perStyle = int.Parse(args[++i]);
            break;
        case "--out":
            outPath = args[++i];
            break;
    }
}

SampleIndex index;
using (FileStream stream = File.OpenRead(catalogPath))
{
    index = new SampleIndex(SampleCatalogSerializer.Deserialize(stream).Samples);
}

Console.Error.WriteLine($"Catalog: {index.Count} samples. Styles: {StyleProfiles.All.Count}.");
Dictionary<string, List<StylePreset>> result = [];
foreach (StyleProfile profile in StyleProfiles.All)
{
    List<(LaunchpadRecipe Recipe, double Score)> candidates = [];
    int[] tempos = [.. Enumerable.Range(0, 4).Select(i => profile.Tempo.Min + ((profile.Tempo.Max - profile.Tempo.Min) * i / 3)).Distinct()];
    foreach (string kit in profile.Kits)
    {
        for (int seed = 1; seed <= seeds; seed++)
        {
            // Half the tries let the generator choose the tempo (it prefers tempos the bank has loops at), half fix one.
            int? tempo = seed % 2 == 0 ? tempos[seed / 2 % tempos.Length] : null;
            try
            {
                LaunchpadRecipe recipe = LaunchpadGenerator.Generate(new LaunchpadGenerationRequest(profile.Key, (seed * 7919) + kit.Length, tempo, null, kit), index);
                candidates.Add((recipe, Score(recipe, index)));
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine($"  {profile.Key}/{kit}/{seed}: {ex.Message}");
            }
        }
    }

    List<StylePreset> chosen = [];
    HashSet<string> kits = [];
    HashSet<string> ids = [];
    foreach ((LaunchpadRecipe recipe, double score) in candidates.OrderByDescending(c => c.Score).ThenBy(c => c.Recipe.Seed))
    {
        if (chosen.Count >= perStyle)
        {
            break;
        }

        // Variety first: one preset per kit while there are kits left.
        bool isNewKit = kits.Add(recipe.Kit);
        if (!isNewKit && kits.Count < profile.Kits.Count)
        {
            continue;
        }

        string tonic = recipe.Key.Tonic.Name(recipe.Key.PrefersFlats).ToLowerInvariant().Replace("#", "sharp", StringComparison.Ordinal);
        string id = $"{profile.Key}-{recipe.Kit}-{tonic}{(recipe.Key.IsMinor ? "m" : string.Empty)}-{recipe.Tempo}";
        if (!ids.Add(id))
        {
            continue;
        }

        string kitName = Taxonomies.Kits.TryGet(recipe.Kit, out KitConcept? concept) ? concept.DisplayName : recipe.Kit;
        chosen.Add(new StylePreset(id, $"{profile.Name} ({kitName})", recipe.Seed, recipe.Tempo, recipe.Key.Name, recipe.Kit));
        Console.Error.WriteLine($"{profile.Key,-10} {score,6:0.0}  {recipe.Title}  [{recipe.Kit}, seed {recipe.Seed}]");
    }

    result[profile.Key] = chosen;
}

string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
if (outPath is null)
{
    Console.WriteLine(json);
}
else
{
    File.WriteAllText(outPath, json);
    Console.Error.WriteLine($"Wrote {result.Values.Sum(list => list.Count)} presets to {outPath}.");
}

return 0;

// How much a setup gives to play: pads, with loops and harmony counting extra, and a bonus for every distinct instrument and
// every well-stocked drum lane.
static double Score(LaunchpadRecipe recipe, SampleIndex index)
{
    double pads = recipe.Fills.Sum(fill => fill.Sounds.Count);
    double loops = recipe.Fills.Where(fill => fill.Loop).Sum(fill => fill.Sounds.Count);
    double harmony = recipe.Fills.Where(fill => fill.Bank == 2).Sum(fill => fill.Sounds.Count);
    int instruments = recipe.Fills.SelectMany(fill => fill.Sounds).Select(sound => index.Find(sound.SampleId)!.Classification.Instrument.Value).Distinct().Count();
    int fullKitLanes = Enumerable.Range(0, 8).Count(lane => recipe.SoundsAt(0, lane).Count >= 6);
    return pads + (3 * loops) + (1.5 * harmony) + (2 * instruments) + (2 * fullKitLanes);
}

static string FindRepoRoot()
{
    for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "OvertonesPlayground.slnx")))
        {
            return directory.FullName;
        }
    }

    return Directory.GetCurrentDirectory();
}
