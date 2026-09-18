using OvertonesPlayground.Models;

namespace OvertonesPlayground.Services.Interfaces;

///<summary>
///Renders a <see cref="MixProject"/>'s tracks down to a single new WAV file - the shared engine behind the
///Multi-Track page, Merge, and Mix. Non-destructive: reads every source clip and writes a brand new file, leaving
///the originals untouched.
///</summary>
///<remarks>
///Every clip is resampled/channel-converted to a common format before mixing, so tracks can freely combine clips of
///different sample rates or mono/stereo clips. If any track in the project is soloed, only soloed tracks are
///included; otherwise every non-muted track is included.
///</remarks>
public interface IMixdownService
{
    #region Public methods
    ///<summary>
    ///Mixes every clip on every included track of <paramref name="project"/> down to a single new WAV file and
    ///returns its path.
    ///</summary>
    ///<exception cref="ArgumentNullException"><paramref name="project"/> is null.</exception>
    ///<exception cref="ArgumentException"><paramref name="outputName"/> is null, empty, or whitespace.</exception>
    ///<exception cref="InvalidOperationException"><paramref name="project"/> has no audible clips to render.</exception>
    Task<string> RenderAsync(MixProject project, string outputName);
    #endregion
}
