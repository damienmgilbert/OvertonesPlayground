using OvertonesPlayground.Models;
using OvertonesPlayground.Ontology.Model;
using OvertonesPlayground.Ontology.Querying;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.Services.Implementations;

///<inheritdoc cref="ILaunchpadExampleService"/>
public sealed class LaunchpadExampleService : ILaunchpadExampleService
{
    #region Constants
    ///<summary>
    ///The folder, under the app's data directory, that the sounds of loaded setups are copied to. It is not the library's folder,
    ///so clearing the library does not take a setup's sounds away.
    ///</summary>
    internal const string SamplesFolderName = "LaunchpadSamples";

    ///<summary>How many sounds are converted at once when a setup is built.</summary>
    private static readonly int CopyParallelism = Math.Clamp(Environment.ProcessorCount / 2, 2, 4);

    ///<summary>The fewest sounds a kit needs to be offered for a kit swap.</summary>
    private const int MinKitSize = 12;
    #endregion

    #region Fields
    private readonly ISampleAssetStore _assets;
    private readonly ISampleCatalogService _catalog;
    private readonly IFileSystem _fileSystem;
    #endregion

    #region Constructors
    ///<summary>
    ///Creates the service.
    ///</summary>
    public LaunchpadExampleService(ISampleCatalogService catalog, ISampleAssetStore assets, IFileSystem fileSystem)
    {
        _catalog = catalog;
        _assets = assets;
        _fileSystem = fileSystem;
    }
    #endregion

    #region Private methods
    ///<summary>
    ///Builds a recipe once and makes its sounds real files (see the other overload).
    ///</summary>
    private Task<LaunchpadProject> MaterializeAsync(Func<SampleIndex, LaunchpadRecipe> recipe, CancellationToken cancellationToken)
    {
        LaunchpadRecipe? built = null;
        return MaterializeAsync((index, pathOf) => (built ??= recipe(index)).Build(index, pathOf), cancellationToken);
    }

    ///<summary>
    ///Builds a setup with <paramref name="build"/> and makes its sounds real files. It builds once with the sounds' asset names as
    ///their paths to learn which sounds the setup needs, copies those out of the app package (a sound already copied is reused),
    ///then builds again with the real files. The build is deterministic, so both come out the same.
    ///</summary>
    ///<remarks>
    ///All of it runs off the UI thread: building a setup and converting its sounds to 16-bit take a few seconds on a tablet, and
    ///the pads must keep drawing meanwhile. The sounds are converted <see cref="CopyParallelism"/> at a time, which is far below the
    ///asset store's cache size, so no copy is trimmed from the cache before it is converted.
    ///</remarks>
    private Task<LaunchpadProject> MaterializeAsync(Func<SampleIndex, Func<Sample, string>, LaunchpadProject> build, CancellationToken cancellationToken) =>
        Task.Run(
        async () =>
        {
            SampleIndex index = await _catalog.GetIndexAsync(cancellationToken).ConfigureAwait(false);
            LaunchpadProject draft = build(index, sample => sample.Id);
            List<string> needed =
            [
                .. draft.Pads.Select(pad => pad.ClipPath)
                    .Concat(draft.Sequence.Tracks.Select(track => track.ClipPath))
                    .OfType<string>()
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal),
            ];

            string directory = Path.Combine(_fileSystem.AppDataDirectory, SamplesFolderName);
            System.Collections.Concurrent.ConcurrentDictionary<string, string> paths = new(StringComparer.Ordinal);
            ParallelOptions options = new() { MaxDegreeOfParallelism = CopyParallelism, CancellationToken = cancellationToken };
            await Parallel.ForEachAsync(needed, options, async (assetName, token) => paths[assetName] = await CopiedAsync(assetName, directory, token).ConfigureAwait(false)).ConfigureAwait(false);
            return build(index, sample => paths[sample.Id]);
        },
        cancellationToken);

    private async Task<string> CopiedAsync(string assetName, string directory, CancellationToken cancellationToken)
    {
        string existing = Path.Combine(directory, assetName);
        return File.Exists(existing) ? existing : await _assets.CopyToAsync(assetName, directory, cancellationToken);
    }
    #endregion

    #region Public methods
    ///<inheritdoc/>
    public async Task<LaunchpadProject> CreateAsync(string exampleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exampleId);
        if (Presets.Any(preset => preset.Id == exampleId))
        {
            LaunchpadProject preset = await MaterializeAsync(index => LaunchpadGenerator.Preset(exampleId, index), cancellationToken);
            preset.Origin = LaunchpadProjectOrigin.Example;
            return preset;
        }

        if (Examples.All(example => example.Id != exampleId))
        {
            throw new ArgumentException($"There is no Launchpad example called '{exampleId}'.", nameof(exampleId));
        }

        LaunchpadProject project = await MaterializeAsync((index, pathOf) => LaunchpadExamples.Build(exampleId, index, pathOf), cancellationToken);
        project.Origin = LaunchpadProjectOrigin.Example;
        return project;
    }

    ///<inheritdoc/>
    public async Task<LaunchpadProject> CreateLessonAsync(string lessonId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lessonId);
        if (!LaunchpadLessons.Ids.Contains(lessonId))
        {
            throw new ArgumentException($"There is no Launchpad lesson setup called '{lessonId}'.", nameof(lessonId));
        }

        return await MaterializeAsync((index, pathOf) => LaunchpadLessons.Build(lessonId, index, pathOf), cancellationToken);
    }

    ///<inheritdoc/>
    public async Task<string> PrepareSampleAsync(string sampleName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sampleName);
        SampleIndex index = await _catalog.GetIndexAsync(cancellationToken);
        Sample sample = index.Find(sampleName + ".wav") ?? throw new ArgumentException($"The sound bank has no sound called '{sampleName}'.", nameof(sampleName));
        return await CopiedAsync(sample.Id, Path.Combine(_fileSystem.AppDataDirectory, SamplesFolderName), cancellationToken);
    }

    ///<inheritdoc/>
    public Task WarmUpAsync(CancellationToken cancellationToken = default) =>
        Task.Run(
        async () =>
        {
            SampleIndex index = await _catalog.GetIndexAsync(cancellationToken).ConfigureAwait(false);
            LaunchpadGenerator.WarmUp(index);
        },
        cancellationToken);

    ///<inheritdoc/>
    public async Task<LaunchpadGeneratedProject> GenerateAsync(LaunchpadGenerationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        LaunchpadRecipe? recipe = null;
        LaunchpadProject project = await MaterializeAsync(index => recipe = LaunchpadGenerator.Generate(request, index), cancellationToken);
        return new LaunchpadGeneratedProject(project, recipe!.Title, recipe.Description);
    }

    ///<inheritdoc/>
    public async Task<IReadOnlyList<LaunchpadSuggestion>> SuggestAsync(LaunchpadProject project, int bank, int padIndex, int count, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        SampleIndex index = await _catalog.GetIndexAsync(cancellationToken);
        return await Task.Run(() => LaunchpadSuggestions.Suggest(project, bank, padIndex, count, index), cancellationToken);
    }

    ///<inheritdoc/>
    public async Task<IReadOnlyList<LaunchpadPadAssignment>> SuggestColumnAsync(LaunchpadProject project, int bank, int column, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        SampleIndex index = await _catalog.GetIndexAsync(cancellationToken);
        return await Task.Run(() => LaunchpadSuggestions.FillColumn(project, bank, column, index), cancellationToken);
    }

    ///<inheritdoc/>
    public async Task<IReadOnlyList<LaunchpadPadAssignment>> SuggestKitSwapAsync(LaunchpadProject project, int bank, string kitKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(kitKey);
        SampleIndex index = await _catalog.GetIndexAsync(cancellationToken);
        return await Task.Run(() => LaunchpadSuggestions.SwapKit(project, bank, kitKey, index), cancellationToken);
    }

    ///<inheritdoc/>
    public async Task<IReadOnlyList<LaunchpadKitInfo>> GetKitsAsync(CancellationToken cancellationToken = default)
    {
        SampleIndex index = await _catalog.GetIndexAsync(cancellationToken);
        return [.. index.GetKits().Where(kit => kit.Members.Count >= MinKitSize).Select(kit => new LaunchpadKitInfo(kit.Concept.Key, kit.Concept.DisplayName, kit.Members.Count))];
    }
    #endregion

    #region Public properties
    ///<inheritdoc/>
    public IReadOnlyList<LaunchpadExampleInfo> Examples => LaunchpadExamples.All;

    ///<inheritdoc/>
    public IReadOnlyList<LaunchpadExampleInfo> Presets => LaunchpadGenerator.Presets;

    ///<inheritdoc/>
    public IReadOnlyList<LaunchpadStyleInfo> Styles => LaunchpadGenerator.Styles;
    #endregion
}
