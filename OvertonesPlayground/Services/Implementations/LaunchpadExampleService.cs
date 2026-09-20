using OvertonesPlayground.Models;
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

    #region Public methods
    ///<inheritdoc/>
    public async Task<LaunchpadProject> CreateAsync(string exampleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exampleId);
        if (Examples.All(example => example.Id != exampleId))
        {
            throw new ArgumentException($"There is no Launchpad example called '{exampleId}'.", nameof(exampleId));
        }

        SampleIndex index = await _catalog.GetIndexAsync(cancellationToken);

        // Build once with the sounds' asset names as their paths to learn which sounds the setup needs, copy those out of the
        // app package, then build again with the real files. The build is deterministic, so both come out the same.
        LaunchpadProject draft = LaunchpadExamples.Build(exampleId, index, sample => sample.Id);
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
            string existing = Path.Combine(directory, assetName);
            paths[assetName] = File.Exists(existing) ? existing : await _assets.CopyToAsync(assetName, directory, cancellationToken);
        }

        return LaunchpadExamples.Build(exampleId, index, sample => paths[sample.Id]);
    }
    #endregion

    #region Public properties
    ///<inheritdoc/>
    public IReadOnlyList<LaunchpadExampleInfo> Examples => LaunchpadExamples.All;
    #endregion
}
