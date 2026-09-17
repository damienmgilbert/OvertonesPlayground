using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.Services.Implementations;

/// <inheritdoc cref="IPermissionsService" />
public class PermissionsService : IPermissionsService
{
    /// <inheritdoc />
    public Task<bool> EnsureMicrophonePermissionAsync() =>
        EnsureGrantedAsync<Permissions.Microphone>();

    /// <inheritdoc />
    public Task<bool> EnsureAudioLibraryPermissionAsync() =>
        EnsureGrantedAsync<Permissions.StorageRead>();

    /// <summary>Checks a permission's status and, if not already granted, prompts the user for it.</summary>
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
