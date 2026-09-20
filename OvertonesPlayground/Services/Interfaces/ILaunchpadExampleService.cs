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

    ///<summary>
    ///Builds the setup a tutorial starts from (one of <c>groove</c>, <c>beat</c> or <c>keys</c>), the same way <see cref="CreateAsync"/> builds an
    ///example: the sounds are copied out of the app package once and reused. Lessons are not listed in <see cref="Examples"/>.
    ///</summary>
    ///<exception cref="ArgumentException">There is no such lesson setup.</exception>
    ///<exception cref="InvalidDataException">The sound bank catalog can't be read.</exception>
    Task<LaunchpadProject> CreateLessonAsync(string lessonId, CancellationToken cancellationToken = default);

    ///<summary>
    ///Copies one sound of the bank into the folder the setups' sounds are kept in (unless it is there already) and returns the path
    ///of the copy, so a tutorial can put a sound from the Sound Bank on a pad.
    ///</summary>
    ///<param name="sampleName">The sound's name in the Sound Bank, without the file extension.</param>
    ///<exception cref="ArgumentException">The bank has no such sound.</exception>
    Task<string> PrepareSampleAsync(string sampleName, CancellationToken cancellationToken = default);
    #endregion

    #region Properties
    ///<summary>
    ///The setups that can be loaded.
    ///</summary>
    IReadOnlyList<LaunchpadExampleInfo> Examples { get; }
    #endregion
}
