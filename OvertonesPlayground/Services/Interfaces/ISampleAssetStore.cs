namespace OvertonesPlayground.Services.Interfaces;

///<summary>
///Turns a bundled sample (an Android asset, which is not a real file) into a file on disk. Playback, the Launchpad, the Mixer
///and the editors all take file paths, so a sample has to be copied out of the package before any of them can use it.
///</summary>
public interface ISampleAssetStore
{
    #region Methods
    ///<summary>
    ///Returns the path of a cached copy of the bundled asset <paramref name="assetName"/>, copying it out of the app package
    ///the first time it is asked for. Only a bounded number of recently used copies are kept.
    ///</summary>
    ///<param name="assetName">The asset's logical name, which is the file name, for example <c>Kick 909 DMX 1.wav</c>.</param>
    ///<param name="cancellationToken">Cancels the copy.</param>
    ///<exception cref="FileNotFoundException">There is no such asset in the package.</exception>
    Task<string> GetLocalPathAsync(string assetName, CancellationToken cancellationToken = default);

    ///<summary>
    ///Copies the bundled asset <paramref name="assetName"/> into <paramref name="destinationDirectory"/> as a permanent file
    ///(unlike <see cref="GetLocalPathAsync"/>, whose copies can be evicted) and returns its path. If a file of that name is
    ///already there, a numbered name is used instead, so nothing is overwritten. The file is 16-bit PCM, the only format the
    ///app's editors read: 24-bit, extensible and float samples are converted (16-bit ones are copied unchanged).
    ///</summary>
    Task<string> CopyToAsync(string assetName, string destinationDirectory, CancellationToken cancellationToken = default);
    #endregion
}
