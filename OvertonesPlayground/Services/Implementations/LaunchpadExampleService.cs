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
    ///Builds a setup with <paramref name="build"/> and makes its sounds real files. It builds once with the sounds' asset names as
    ///their paths to learn which sounds the setup needs, copies those out of the app package (a sound already copied is reused),
    ///then builds again with the real files. The build is deterministic, so both come out the same.
    ///</summary>
    private async Task<LaunchpadProject> MaterializeAsync(Func<SampleIndex, Func<Sample, string>, LaunchpadProject> build, CancellationToken cancellationToken)
    {
        SampleIndex index = await _catalog.GetIndexAsync(cancellationToken);

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
        Dictionary<string, string> paths = new(StringComparer.Ordinal);
        foreach (string assetName in needed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            paths[assetName] = await CopiedAsync(assetName, directory, cancellationToken);
        }

        return build(index, sample => paths[sample.Id]);
    }

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
        if (Examples.All(example => example.Id != exampleId))
        {
            throw new ArgumentException($"There is no Launchpad example called '{exampleId}'.", nameof(exampleId));
        }

        return await MaterializeAsync((index, pathOf) => LaunchpadExamples.Build(exampleId, index, pathOf), cancellationToken);
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
    #endregion

    #region Public properties
    ///<inheritdoc/>
    public IReadOnlyList<LaunchpadExampleInfo> Examples => LaunchpadExamples.All;
    #endregion
}
