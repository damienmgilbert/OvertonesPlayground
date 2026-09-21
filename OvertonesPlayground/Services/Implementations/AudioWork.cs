namespace OvertonesPlayground.Services.Implementations;

///<summary>
///The one way the audio services keep heavy work off the UI thread. Reading a WAV, applying an effect, synthesizing a drum
///or mixing tracks is pure number-crunching that can take long enough to freeze the page that asked for it.
///</summary>
///<remarks>
///Every public entry point of a service that does such work passes its whole body through <see cref="RunAsync{T}(Func{Task{T}})"/>.
///Running the entire body on the thread pool, rather than only the first hop, means nothing after a later <c>await</c> can
///resume on the UI thread, so the code inside needs no <c>ConfigureAwait(false)</c> and adding a new step can't break the
///guarantee by forgetting one.
///</remarks>
internal static class AudioWork
{
    #region Public methods
    ///<summary>
    ///Runs <paramref name="work"/> on the thread pool and returns its result.
    ///</summary>
    public static Task<T> RunAsync<T>(Func<Task<T>> work) => Task.Run(work);

    ///<summary>
    ///Runs <paramref name="work"/> on the thread pool.
    ///</summary>
    public static Task RunAsync(Func<Task> work) => Task.Run(work);

    ///<summary>
    ///Runs the synchronous <paramref name="work"/> on the thread pool and returns its result.
    ///</summary>
    public static Task<T> RunAsync<T>(Func<T> work) => Task.Run(work);
    #endregion
}
