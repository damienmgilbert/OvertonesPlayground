using System.Diagnostics;
using OvertonesPlayground.Ontology.Catalog;
using OvertonesPlayground.Ontology.Querying;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.Services.Implementations;

///<inheritdoc cref="ISampleCatalogService"/>
public class SampleCatalogService : ISampleCatalogService
{
    #region Constants
    ///<summary>
    ///Logical name of the catalog asset. It sits at the root of <c>Resources/Raw</c> rather than in a folder because the
    ///asset's logical name is built from <c>%(RecursiveDir)</c>, which uses backslashes on a Windows build machine.
    ///</summary>
    public const string CatalogAssetName = "sample-catalog.json";
    #endregion

    #region Fields
    private readonly IFileSystem _fileSystem;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SampleIndex? _index;
    #endregion

    #region Constructors
    public SampleCatalogService(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }
    #endregion

    #region Public methods
    ///<inheritdoc/>
    public async Task<SampleIndex> GetIndexAsync(CancellationToken cancellationToken = default)
    {
        SampleIndex? loaded = _index;
        if (loaded is not null)
        {
            return loaded;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_index is null)
            {
                Stopwatch clock = Stopwatch.StartNew();

                // An Android asset stream isn't seekable and hands out bytes slowly, so take it into memory first.
                using MemoryStream copy = new();
                try
                {
                    using Stream asset = await _fileSystem.OpenAppPackageFileAsync(CatalogAssetName);
                    await asset.CopyToAsync(copy, cancellationToken);
                }
                catch (FileNotFoundException ex)
                {
                    throw new InvalidDataException($"The sound bank catalog '{CatalogAssetName}' is not in the app package. Run tools/SampleAnalyzer.", ex);
                }

                copy.Position = 0;
                SampleCatalog catalog = await Task.Run(() => SampleCatalogSerializer.Deserialize(copy), cancellationToken);
                _index = await Task.Run(() => new SampleIndex(catalog.Samples), cancellationToken);
                LoadDuration = clock.Elapsed;
            }

            return _index;
        }
        finally
        {
            _ = _gate.Release();
        }
    }
    #endregion

    #region Public properties
    ///<inheritdoc/>
    public bool IsLoaded => _index is not null;

    ///<inheritdoc/>
    public TimeSpan LoadDuration { get; private set; }
    #endregion
}
