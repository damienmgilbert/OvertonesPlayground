namespace OvertonesPlayground.Tests.TestSupport;

///<summary>
///The committed sample catalog (<c>Resources/Raw/sample-catalog.json</c>), loaded once, for tests that check what the
///real sound bank can do rather than what a made-up sample can. It reads only the JSON: no audio is decoded.
///</summary>
internal static class RealCatalog
{
    #region Fields
    private static readonly Lazy<SampleCatalog> _catalog = new(Load);
    private static readonly Lazy<SampleIndex> _index = new(() => new SampleIndex(_catalog.Value.Samples));
    #endregion

    #region Private methods
    private static SampleCatalog Load()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string catalog = Path.Combine(directory.FullName, "OvertonesPlayground", "Resources", "Raw", "sample-catalog.json");
            if (File.Exists(Path.Combine(directory.FullName, "OvertonesPlayground.slnx")) && File.Exists(catalog))
            {
                using FileStream stream = File.OpenRead(catalog);
                return SampleCatalogSerializer.Deserialize(stream);
            }
        }

        throw new FileNotFoundException($"Could not find OvertonesPlayground/Resources/Raw/sample-catalog.json above {AppContext.BaseDirectory}");
    }
    #endregion

    #region Public properties
    ///<summary>
    ///The whole catalog.
    ///</summary>
    public static SampleCatalog Catalog => _catalog.Value;

    ///<summary>
    ///An index over it, the same one the app builds.
    ///</summary>
    public static SampleIndex Index => _index.Value;
    #endregion
}
