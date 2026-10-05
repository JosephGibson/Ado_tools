using System.Text;

namespace AdoToolkit.Core.Update;

// File system boundaries of the updater, mirroring Install-AdoToolkit.ps1: links are never traversed,
// and only folders the update made or owns are deleted.
internal static class UpdateGuards
{
    private static readonly EnumerationOptions DirectChildren = new()
    {
        RecurseSubdirectories = false, AttributesToSkip = 0, IgnoreInaccessible = false,
        ReturnSpecialDirectories = false, MatchType = MatchType.Simple,
    };

    // A symbolic link or a junction. A cloud-file placeholder, such as a OneDrive-redirected
    // Documents folder, is a reparse point without a target and is not a link (Assert-NoLink).
    internal static bool IsLink(FileSystemInfo item) =>
        item.Attributes.HasFlag(FileAttributes.ReparsePoint) && item.LinkTarget is not null;

    // The folder or its first existing ancestor that is a link, or null.
    internal static string? FindLink(string folder)
    {
        for (DirectoryInfo? current = new(folder); current is not null; current = current.Parent)
            if (current.Exists && IsLink(current)) return current.FullName;
        return null;
    }

    // The first link below the folder, found without following any link, or null (Assert-NoLinkInside).
    internal static string? FindLinkInside(string folder)
    {
        Queue<DirectoryInfo> pending = new([new DirectoryInfo(folder)]);
        while (pending.TryDequeue(out DirectoryInfo? current))
            foreach (FileSystemInfo item in current.EnumerateFileSystemInfos("*", DirectChildren))
            {
                if (IsLink(item)) return item.FullName;
                if (item is DirectoryInfo child) pending.Enqueue(child);
            }
        return null;
    }

    // The files below the folder as relative paths with '/', for a folder known to hold no link.
    internal static List<string> RelativeFiles(string folder)
    {
        List<string> files = [];
        Queue<DirectoryInfo> pending = new([new DirectoryInfo(folder)]);
        while (pending.TryDequeue(out DirectoryInfo? current))
            foreach (FileSystemInfo item in current.EnumerateFileSystemInfos("*", DirectChildren))
            {
                if (item is DirectoryInfo child) pending.Enqueue(child);
                else files.Add(Path.GetRelativePath(folder, item.FullName).Replace('\\', '/'));
            }
        return files;
    }

    // Deletes a folder that the update made or owns. A link inside is left in place, and so is its
    // parent; any failure leaves the rest (Remove-Leftover). Returns whether the folder is gone.
    internal static bool TryDeleteTree(string folder)
    {
        try
        {
            DirectoryInfo root = new(folder);
            if (!root.Exists) return true;
            return !IsLink(root) && DeleteTree(root);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool DeleteTree(DirectoryInfo folder)
    {
        bool complete = true;
        foreach (FileSystemInfo item in folder.EnumerateFileSystemInfos("*", DirectChildren).ToArray())
        {
            if (IsLink(item)) complete = false;
            else if (item is DirectoryInfo child) complete &= DeleteTree(child);
            else item.Delete();
        }
        if (complete) folder.Delete(recursive: false);
        return complete;
    }

    // ERROR_DISK_FULL and ERROR_HANDLE_DISK_FULL.
    internal static bool IsDiskFull(Exception error) => error.HResult is unchecked((int)0x80070070) or unchecked((int)0x80070027);

    // ERROR_SHARING_VIOLATION and ERROR_LOCK_VIOLATION.
    internal static bool IsSharingViolation(Exception error) => error.HResult is unchecked((int)0x80070020) or unchecked((int)0x80070021);

    // A cheap refusal before extraction. A share or an unreadable drive is not checked; writing then
    // fails as ERROR_DISK_FULL if the space runs out.
    internal static void EnsureFreeSpace(string folder, long bytes, long margin, Version version, CultureInfo culture)
    {
        long free;
        try
        {
            string? root = Path.GetPathRoot(Path.GetFullPath(folder));
            if (string.IsNullOrEmpty(root)) return;
            free = new DriveInfo(root).AvailableFreeSpace;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return;
        }
        if (free < bytes + margin)
            throw new AdoFileOutputException(Messages.Get(AdoMessage.UpdateDiskFull, culture, folder, UpdateHttp.VersionText(version)));
    }

    // Remote text in a message: control characters become spaces, and the text is cut.
    internal static string Printable(string? text, int limit)
    {
        if (string.IsNullOrEmpty(text)) return "";
        StringBuilder result = new(Math.Min(text.Length, limit));
        foreach (char character in text)
        {
            if (result.Length >= limit) break;
            result.Append(char.IsControl(character) ? ' ' : character);
        }
        return result.ToString();
    }
}
