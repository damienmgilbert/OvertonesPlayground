namespace OvertonesPlayground.Services.Implementations;

///<summary>
///Brings a file picked by <see cref="IFilePicker"/> into app storage. Shared by every service that stages a picked file
///before working on it (the audio library's import, the video converter's video pick), so the picker-copy quirks below
///are handled once.
///</summary>
internal static class PickedFileStager
{
    ///<summary>
    ///Bytes read and written per step when copying a picked file; big enough that a large file isn't hundreds of
    ///thousands of tiny writes.
    ///</summary>
    private const int CopyBufferSize = 1024 * 1024;

    ///<summary>
    ///Brings <paramref name="result"/> to <paramref name="destination"/>, reporting the fraction copied (0 to 1) to
    ///<paramref name="report"/>. Where possible it moves the copy the picker already made instead of copying it again.
    ///</summary>
    public static async Task StageAsync(FileResult result, string destination, IFileSystem fileSystem, Action<double> report)
    {
        if (TryMovePickerCopy(result, destination, fileSystem))
        {
            report(1);
            return;
        }

        await using Stream source = await result.OpenReadAsync();
        await using FileStream dest = File.Create(destination);

        // Not every stream reports its length, in which case there is nothing to measure the copy against.
        long total = source.CanSeek ? source.Length : 0;
        byte[] buffer = new byte[CopyBufferSize];
        long copied = 0;
        int lastPercent = -1;
        int read;
        while ((read = await source.ReadAsync(buffer)) > 0)
        {
            await dest.WriteAsync(buffer.AsMemory(0, read));
            copied += read;

            int percent = total > 0 ? (int)Math.Min(100, copied * 100 / total) : lastPercent;
            bool hasAdvanced = percent != lastPercent;
            if (hasAdvanced)
            {
                lastPercent = percent;
                report(percent / 100.0);
            }
        }
    }

    ///<summary>
    ///Returns <paramref name="path"/> with any symbolic link among its folders followed, so that two spellings of the
    ///same location come out identical. On Android /data/data and /data/user/0 are the same folder that way.
    ///</summary>
    private static string ResolveFolderLinks(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string? folder = Path.GetDirectoryName(fullPath);
        while (!string.IsNullOrEmpty(folder))
        {
            // A drive or filesystem root is never a link, and on Windows asking one to resolve throws.
            bool isRoot = string.Equals(folder, Path.GetPathRoot(folder), StringComparison.Ordinal);
            if (isRoot)
            {
                break;
            }

            FileSystemInfo? target = new DirectoryInfo(folder).ResolveLinkTarget(returnFinalTarget: true);
            if (target is not null)
            {
                return Path.Combine(target.FullName, Path.GetRelativePath(folder, fullPath));
            }

            folder = Path.GetDirectoryName(folder);
        }

        return fullPath;
    }

    ///<summary>
    ///On Android the picker copies what the user chose into the app's cache folder before handing it back. If that is
    ///what <paramref name="result"/> points at, moves it to <paramref name="destination"/> (a rename, however big the
    ///file) and returns true. A file anywhere else may be the user's own, so it is never moved; returns false and leaves
    ///it alone.
    ///</summary>
    private static bool TryMovePickerCopy(FileResult result, string destination, IFileSystem fileSystem)
    {
        string? pickedPath = result.FullPath;
        bool isMissing = string.IsNullOrEmpty(pickedPath) || !File.Exists(pickedPath);
        if (isMissing)
        {
            return false;
        }

        try
        {
            // The picker and FileSystem.CacheDirectory can name the same folder differently (/data/data/... and
            // /data/user/0/...), so compare the folders once their links are followed.
            string cacheFolder = ResolveFolderLinks(fileSystem.CacheDirectory) + Path.DirectorySeparatorChar;
            bool isPickerCopy = ResolveFolderLinks(pickedPath).StartsWith(cacheFolder, StringComparison.Ordinal);
            if (!isPickerCopy)
            {
                return false;
            }

            File.Move(pickedPath, destination);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Fall back to copying the stream.
            return false;
        }
    }
}
