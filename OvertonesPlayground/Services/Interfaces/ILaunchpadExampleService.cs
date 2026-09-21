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
    ///Builds the setup <paramref name="exampleId"/> (an example or a style preset): copies the sounds it uses out of the app package into permanent files (once;
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

    ///<summary>
    ///Loads the sound bank catalog and prepares the generator in the background, so the first setup built after the Launchpad
    ///opens does not wait for them. Calling it again does nothing more.
    ///</summary>
    ///<exception cref="InvalidDataException">The sound bank catalog can't be read.</exception>
    Task WarmUpAsync(CancellationToken cancellationToken = default);

    ///<summary>
    ///Generates a new setup in a style (see <see cref="Styles"/>) and copies its sounds out of the app package, the same way
    ///<see cref="CreateAsync"/> does. The same request always gives the same setup.
    ///</summary>
    ///<exception cref="KeyNotFoundException">There is no such style.</exception>
    ///<exception cref="InvalidOperationException">The generator couldn't make a setup that passes its checks.</exception>
    ///<exception cref="InvalidDataException">The sound bank catalog can't be read.</exception>
    Task<LaunchpadGeneratedProject> GenerateAsync(LaunchpadGenerationRequest request, CancellationToken cancellationToken = default);

    ///<summary>
    ///Up to <paramref name="count"/> Sound Bank sounds for pad <paramref name="padIndex"/> of bank <paramref name="bank"/> of
    ///<paramref name="project"/>, best first, each with why it suits: in the project's key, at its tempo, from the kits it uses, and
    ///what the pad's column holds. For a pad that has a sound, they are alternatives like it.
    ///</summary>
    Task<IReadOnlyList<LaunchpadSuggestion>> SuggestAsync(LaunchpadProject project, int bank, int padIndex, int count, CancellationToken cancellationToken = default);

    ///<summary>
    ///Sounds for the empty pads of column <paramref name="column"/> of bank <paramref name="bank"/>, chosen together so they
    ///differ from each other.
    ///</summary>
    Task<IReadOnlyList<LaunchpadPadAssignment>> SuggestColumnAsync(LaunchpadProject project, int bank, int column, CancellationToken cancellationToken = default);

    ///<summary>
    ///For each drum on bank <paramref name="bank"/>, the nearest sound of the same instrument from kit <paramref name="kitKey"/>.
    ///</summary>
    Task<IReadOnlyList<LaunchpadPadAssignment>> SuggestKitSwapAsync(LaunchpadProject project, int bank, string kitKey, CancellationToken cancellationToken = default);

    ///<summary>
    ///The drum kits of the Sound Bank that have enough sounds to swap to, largest first.
    ///</summary>
    Task<IReadOnlyList<LaunchpadKitInfo>> GetKitsAsync(CancellationToken cancellationToken = default);
    #endregion

    #region Properties
    ///<summary>
    ///The hand-made setups that can be loaded.
    ///</summary>
    IReadOnlyList<LaunchpadExampleInfo> Examples { get; }

    ///<summary>
    ///The ready-made setups of each style (fixed runs of the generator), which <see cref="CreateAsync"/> also builds.
    ///</summary>
    IReadOnlyList<LaunchpadExampleInfo> Presets { get; }

    ///<summary>
    ///The styles <see cref="GenerateAsync"/> can make setups in.
    ///</summary>
    IReadOnlyList<LaunchpadStyleInfo> Styles { get; }
    #endregion
}
