using System.Runtime.Versioning;
using Android.Content;
using Android.Media;
using Android.Provider;
using OvertonesPlayground.Services.Interfaces;
using AndroidApp = Android.App.Application;
using AndroidEnvironment = Android.OS.Environment;

namespace OvertonesPlayground.Platforms.Android.Services;

/// <summary>
/// Exports app-created audio into the shared Music/OvertonesPlayground folder. On Android 10+
/// this goes through MediaStore (scoped storage - no broad storage permission needed); on older
/// OS versions it falls back to writing the public Music directory directly and asking the
/// media scanner to index it.
/// </summary>
public class PublicStorageService : IPublicStorageService
{
    private const string SubFolder = "OvertonesPlayground";

    /// <summary>Maps a file's extension to the MIME type MediaStore should tag it with, defaulting to WAV for
    /// anything unrecognized since that's every format this app wrote before AAC/MP3 export existed.</summary>
    private static string GetMimeType(string filePath)
    {
        string extension = Path.GetExtension(filePath);
        if (string.Equals(extension, ".aac", StringComparison.OrdinalIgnoreCase))
        {
            return "audio/aac";
        }

        if (string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase))
        {
            return "audio/mpeg";
        }

        return "audio/wav";
    }

    /// <inheritdoc />
    public async Task<string?> ExportToMusicAsync(string sourceFilePath, string displayFileName)
    {
        try
        {
            return OperatingSystem.IsAndroidVersionAtLeast(29)
                ? await ExportViaMediaStoreAsync(sourceFilePath, displayFileName)
                : await ExportLegacyAsync(sourceFilePath, displayFileName);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Inserts a pending MediaStore entry, streams the file's bytes into it, then clears the pending flag.</summary>
    [SupportedOSPlatform("android29.0")]
    private static async Task<string?> ExportViaMediaStoreAsync(string sourceFilePath, string displayFileName)
    {
        ContentResolver? resolver = AndroidApp.Context.ContentResolver;
        if (resolver is null)
        {
            return null;
        }

        using ContentValues values = new();
        values.Put(MediaStore.IMediaColumns.DisplayName, displayFileName);
        values.Put(MediaStore.IMediaColumns.MimeType, GetMimeType(sourceFilePath));
        values.Put(MediaStore.IMediaColumns.RelativePath, $"{AndroidEnvironment.DirectoryMusic}/{SubFolder}");
        values.Put(MediaStore.IMediaColumns.IsPending, 1);

        global::Android.Net.Uri? collection = MediaStore.Audio.Media.GetContentUri(MediaStore.VolumeExternalPrimary);
        global::Android.Net.Uri? itemUri = resolver.Insert(collection!, values);
        if (itemUri is null)
        {
            return null;
        }

        await using (System.IO.Stream? output = resolver.OpenOutputStream(itemUri))
        {
            if (output is null)
            {
                return null;
            }

            await using FileStream input = File.OpenRead(sourceFilePath);
            await input.CopyToAsync(output);
        }

        using ContentValues pendingValues = new();
        pendingValues.Put(MediaStore.IMediaColumns.IsPending, 0);
        resolver.Update(itemUri, pendingValues, null, null);

        return $"Music/{SubFolder}/{displayFileName}";
    }

    /// <summary>Pre-Android-10 fallback: writes directly into the public Music directory and asks the media scanner to index it.</summary>
    private static async Task<string?> ExportLegacyAsync(string sourceFilePath, string displayFileName)
    {
        Java.IO.File? musicDir = AndroidEnvironment.GetExternalStoragePublicDirectory(AndroidEnvironment.DirectoryMusic);
        if (musicDir is null)
        {
            return null;
        }

        string targetDir = Path.Combine(musicDir.AbsolutePath, SubFolder);
        Directory.CreateDirectory(targetDir);
        string targetPath = Path.Combine(targetDir, displayFileName);

        await using (FileStream input = File.OpenRead(sourceFilePath))
        await using (FileStream output = File.Create(targetPath))
        {
            await input.CopyToAsync(output);
        }

        MediaScannerConnection.ScanFile(AndroidApp.Context, [targetPath], null, null);
        return $"Music/{SubFolder}/{displayFileName}";
    }
}
