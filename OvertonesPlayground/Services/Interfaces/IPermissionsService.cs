namespace OvertonesPlayground.Services.Interfaces;

public interface IPermissionsService
{
    Task<bool> EnsureMicrophonePermissionAsync();

    Task<bool> EnsureAudioLibraryPermissionAsync();
}
