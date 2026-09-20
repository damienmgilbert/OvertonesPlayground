using OvertonesPlayground.Models;
using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.Services.Implementations;

///<summary>
///Runs a "choose a sound" request by showing the Sound Bank page and waiting for it to report the user's choice.
///</summary>
public class SamplePickerService(INavigationService navigation) : ISamplePickerService
{
    #region Constants
    private const string SoundBankRoute = "//soundbank";
    #endregion

    #region Fields
    private readonly INavigationService _navigation = navigation;
    private string _returnRoute = string.Empty;
    private TaskCompletionSource<AudioClip?>? _pending;
    #endregion

    #region Events
    ///<inheritdoc/>
    public event EventHandler? PickingChanged;
    #endregion

    #region Properties
    ///<inheritdoc/>
    public bool IsPicking => _pending is not null;

    ///<inheritdoc/>
    public string? Prompt { get; private set; }
    #endregion

    #region Public methods
    ///<inheritdoc/>
    public async Task<AudioClip?> PickAsync(string prompt, string returnRoute)
    {
        Abandon();
        TaskCompletionSource<AudioClip?> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending = pending;
        _returnRoute = returnRoute;
        Prompt = prompt;
        PickingChanged?.Invoke(this, EventArgs.Empty);
        await _navigation.GoToAsync(SoundBankRoute);
        return await pending.Task;
    }

    ///<inheritdoc/>
    public async Task CompleteAsync(AudioClip clip)
    {
        string route = _returnRoute;
        if (Finish(clip))
        {
            await _navigation.GoToAsync(route);
        }
    }

    ///<inheritdoc/>
    public async Task CancelAsync()
    {
        string route = _returnRoute;
        if (Finish(null))
        {
            await _navigation.GoToAsync(route);
        }
    }

    ///<inheritdoc/>
    public void Abandon() => _ = Finish(null);
    #endregion

    #region Private methods
    private bool Finish(AudioClip? clip)
    {
        TaskCompletionSource<AudioClip?>? pending = _pending;
        if (pending is null)
        {
            return false;
        }

        _pending = null;
        Prompt = null;
        PickingChanged?.Invoke(this, EventArgs.Empty);
        _ = pending.TrySetResult(clip);
        return true;
    }
    #endregion
}
