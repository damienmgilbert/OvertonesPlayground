namespace OvertonesPlayground.Services.Interfaces;

/// <summary>
/// Wraps the platform's audio focus API so playback ducks/pauses correctly when a phone call,
/// notification sound, or another music app wants the speaker.
/// </summary>
public interface IAudioFocusService
{
    event EventHandler<bool>? FocusChanged;

    bool RequestFocus();

    void AbandonFocus();
}
