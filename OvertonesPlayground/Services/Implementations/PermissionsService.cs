using OvertonesPlayground.Services.Interfaces;

namespace OvertonesPlayground.Services.Implementations;

///<inheritdoc cref="IPermissionsService"/>
public class PermissionsService : IPermissionsService
{
    #region Private methods

    ///<summary>
    ///Checks a permission's status and, if not already granted, prompts the user for it.
    ///</summary>
    private static async Task<bool> EnsureGrantedAsync<TPermission>() where TPermission : Permissions.BasePermission, new()
    {
        PermissionStatus status = await Permissions.CheckStatusAsync<TPermission>();
        bool isGranted = status == PermissionStatus.Granted;
        if (isGranted)
        {
            return true;
        }

        status = await Permissions.RequestAsync<TPermission>();
        bool isGrantedAfterRequest = status == PermissionStatus.Granted;
        return isGrantedAfterRequest;
    }
    #endregion

    #region Public methods
    ///<inheritdoc/>
    public Task<bool> EnsureAudioLibraryPermissionAsync()
    {
        Task<bool> task = EnsureGrantedAsync<Permissions.StorageRead>();
        return task;
    }

    ///<inheritdoc/>
    public Task<bool> EnsureMicrophonePermissionAsync()
    {
        Task<bool> task = EnsureGrantedAsync<Permissions.Microphone>();
        return task;
    }
    #endregion
}
