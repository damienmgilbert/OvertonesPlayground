using OvertonesPlayground.Models;

namespace OvertonesPlayground.Services.Interfaces;

///<summary>
///Lets a page ask the user to choose a sound from the Sound Bank. The Sound Bank page is shown in "pick" mode, and the sound the
///user chooses is copied into their library and handed back as a clip.
///</summary>
public interface ISamplePickerService
{
    #region Events
    ///<summary>
    ///Raised when a pick starts or ends, so the Sound Bank page can switch its pick mode on or off.
    ///</summary>
    event EventHandler? PickingChanged;
    #endregion

    #region Properties
    ///<summary>
    ///True while a pick is waiting for the user to choose.
    ///</summary>
    bool IsPicking { get; }

    ///<summary>
    ///What the pick is for, for example "Choose a sound for pad 3"; null when nothing is being picked.
    ///</summary>
    string? Prompt { get; }
    #endregion

    #region Methods
    ///<summary>
    ///Shows the Sound Bank and waits for the user to choose a sound. Returns null if they cancel or leave the Sound Bank without
    ///choosing. On a choice or a cancel, navigates back to <paramref name="returnRoute"/>.
    ///</summary>
    Task<AudioClip?> PickAsync(string prompt, string returnRoute);

    ///<summary>
    ///Finishes the pick with <paramref name="clip"/> and returns to the page that asked.
    ///</summary>
    Task CompleteAsync(AudioClip clip);

    ///<summary>
    ///Cancels the pick and returns to the page that asked.
    ///</summary>
    Task CancelAsync();

    ///<summary>
    ///Cancels the pick without navigating, because the user has already gone somewhere else.
    ///</summary>
    void Abandon();
    #endregion
}
