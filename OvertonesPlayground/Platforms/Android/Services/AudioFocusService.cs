using Android.Content;
using Android.Media;
using OvertonesPlayground.Services.Interfaces;
using AndroidApp = Android.App.Application;

namespace OvertonesPlayground.Platforms.Android.Services;

/// <summary>
/// Requests/abandons Android audio focus so OvertonesPlayground behaves like a well-behaved
/// music app: it ducks or pauses when a call comes in and reclaims focus afterwards.
/// </summary>
public class AudioFocusService : Java.Lang.Object, IAudioFocusService, AudioManager.IOnAudioFocusChangeListener
{
    private readonly AudioManager? _audioManager;
    private AudioFocusRequestClass? _focusRequest;

    /// <inheritdoc />
    public event EventHandler<bool>? FocusChanged;

    /// <summary>Resolves the system <see cref="AudioManager"/> for the app's context.</summary>
    public AudioFocusService()
    {
        _audioManager = AndroidApp.Context.GetSystemService(Context.AudioService) as AudioManager;
    }

    /// <inheritdoc />
    public bool RequestFocus()
    {
        if (_audioManager is null)
        {
            return false;
        }

        AudioFocusRequest result;

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            using var attributesBuilder = new AudioAttributes.Builder();
            attributesBuilder.SetUsage(AudioUsageKind.Media);
            attributesBuilder.SetContentType(AudioContentType.Music);
            var attributes = attributesBuilder.Build();

            using var focusRequestBuilder = new AudioFocusRequestClass.Builder(AudioFocus.Gain);
            focusRequestBuilder.SetAudioAttributes(attributes!);
            focusRequestBuilder.SetOnAudioFocusChangeListener(this);
            _focusRequest = focusRequestBuilder.Build();

            result = _audioManager.RequestAudioFocus(_focusRequest!);
        }
        else
        {
#pragma warning disable CA1422, CS0618 // legacy API path required on API < 26
            result = _audioManager.RequestAudioFocus(this, global::Android.Media.Stream.Music, AudioFocus.Gain);
#pragma warning restore CA1422, CS0618
        }

        return result == AudioFocusRequest.Granted;
    }

    /// <inheritdoc />
    public void AbandonFocus()
    {
        if (_audioManager is null)
        {
            return;
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(26) && _focusRequest is not null)
        {
            _audioManager.AbandonAudioFocusRequest(_focusRequest);
        }
        else
        {
#pragma warning disable CA1422, CS0618
            _audioManager.AbandonAudioFocus(this);
#pragma warning restore CA1422, CS0618
        }
    }

    /// <summary>Android's callback for focus gain/loss; raises <see cref="FocusChanged"/> for the rest of the app.</summary>
    public void OnAudioFocusChange(AudioFocus focusChange)
    {
        var haveFocus = focusChange == AudioFocus.Gain;
        FocusChanged?.Invoke(this, haveFocus);
    }
}
