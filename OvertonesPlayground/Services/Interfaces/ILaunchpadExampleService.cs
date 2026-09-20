using OvertonesPlayground.Models;

namespace OvertonesPlayground.Services.Interfaces;

///<summary>
///Builds ready-made Launchpad setups from the bundled sound bank. Each one is assembled by rules over what the ontology knows
///about the sounds (role, level, stereo image, low-end content, tempo and key) so the sounds in it complement each other, and it
///uses what the Launchpad can do: a column per instrument role, per-pad levels, loops, Radio, the column mixer, swing and a
///four-track sequencer with velocity and probability.
///</summary>
public interface ILaunchpadExampleService
{
    #region Methods
    ///<summary>
    ///Builds the setup <paramref name="exampleId"/>: copies the sounds it uses out of the app package into permanent files (once;
    ///a sound already copied is reused) and returns a project whose pads and tracks point at them.
    ///</summary>
    ///<exception cref="ArgumentException">There is no such example.</exception>
    ///<exception cref="InvalidDataException">The sound bank catalog can't be read.</exception>
    Task<LaunchpadProject> CreateAsync(string exampleId, CancellationToken cancellationToken = default);
    #endregion

    #region Properties
    ///<summary>
    ///The setups that can be loaded.
    ///</summary>
    IReadOnlyList<LaunchpadExampleInfo> Examples { get; }
    #endregion
}
