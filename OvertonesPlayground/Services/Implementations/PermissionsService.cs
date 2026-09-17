using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.Services.Implementations;

public class PermissionsService : IPermissionsService
{
    public Task<bool> EnsureMicrophonePermissionAsync() =>
        EnsureGrantedAsync<Permissions.Microphone>();

    public Task<bool> EnsureAudioLibraryPermissionAsync() =>
        EnsureGrantedAsync<Permissions.StorageRead>();

    private static async Task<bool> EnsureGrantedAsync<TPermission>()
        where TPermission : Permissions.BasePermission, new()
    {
        var status = await Permissions.CheckStatusAsync<TPermission>();
        if (status == PermissionStatus.Granted)
        {
            return true;
        }

        status = await Permissions.RequestAsync<TPermission>();
        return status == PermissionStatus.Granted;
    }
}
