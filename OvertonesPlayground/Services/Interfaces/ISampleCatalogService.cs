using OvertonesPlayground.Ontology.Querying;

namespace OvertonesPlayground.Services.Interfaces;

///<summary>
///Loads the precomputed description of the bundled sound bank (see <c>tools/SampleAnalyzer</c>) and answers queries about it.
///The catalog is read once, on first use, and kept for the life of the app.
///</summary>
public interface ISampleCatalogService
{
    #region Methods
    ///<summary>
    ///Returns the index over every bundled sample, loading and parsing the catalog asset the first time it is called. Safe to
    ///call from several places at once: they all wait for the same load.
    ///</summary>
    ///<exception cref="InvalidDataException">The catalog asset is missing, unreadable, or from an unsupported schema version.</exception>
    Task<SampleIndex> GetIndexAsync(CancellationToken cancellationToken = default);
    #endregion

    #region Properties
    ///<summary>
    ///Whether the catalog has already been loaded.
    ///</summary>
    bool IsLoaded { get; }

    ///<summary>
    ///How long the last load took (reading the asset, parsing it and building the index); zero before the first load.
    ///</summary>
    TimeSpan LoadDuration { get; }
    #endregion
}
