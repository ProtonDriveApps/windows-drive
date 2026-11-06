using System.Threading;
using Proton.Drive.Sdk.Sync.Windows.FileSystem.Integration;
using Proton.Drive.Sdk.Sync.Windows.Interop;

namespace Proton.Drive.Sdk.Sync.Windows.FileSystem.Watcher;

/// <summary>
/// Fallback for file systems that do not support extended change notifications.
/// </summary>
/// <remarks>
/// <para>
/// ReFS (including Dev Drive) fails <c>ReadDirectoryChangesExW</c> with
/// <c>ReadDirectoryNotifyExtendedInformation</c> with <c>ERROR_INVALID_FUNCTION</c>, while basic
/// notifications work. Basic notifications carry only the action and the relative path, but the sync
/// engine identifies file system objects by ID.
/// </para>
/// <para>
/// For objects that exist, the ID is looked up by path when the notification is processed. The lookup
/// reflects the state at processing time and only ever reports an ID together with the path where the
/// object currently is.
/// </para>
/// <para>
/// For objects that are gone (deletion, old name of a rename) the ID can no longer be looked up. The
/// watcher remembers the last ID it saw for each path and reports those events with it, the same way the
/// extended notifications do. Before reporting a deletion it checks that the remembered object is not
/// still at that path. Whatever cannot be attributed is reported without an ID
/// (<see cref="FileExtendedInfo.FileId"/> is 0), which the event log client turns into a request to
/// re-enumerate the sync root instead of guessing.
/// </para>
/// </remarks>
public partial class FileSystemExtendedWatcher
{
    private const int ErrorInvalidFunction = 1;
    private const int MaxKnownObjects = 100_000;

    private static readonly EnumerationOptions LookUpEnumerationOptions = new()
    {
        AttributesToSkip = default,
        ReturnSpecialDirectories = false,
        BufferSize = 1024,
    };

    // Last known metadata and IDs per path relative to the watched directory. Only used with basic notifications.
    private readonly Dictionary<string, FileExtendedInfo> _knownObjects = new(StringComparer.OrdinalIgnoreCase);

    private volatile bool _useBasicNotifications;

    private enum LookUpResult
    {
        Found,
        NotFound,
        Failed,
    }

    /// <summary>
    /// Gets a value indicating whether the watcher fell back to basic change notifications.
    /// </summary>
    public bool UsesBasicNotifications => _useBasicNotifications;

    private unsafe bool ReadDirectoryChanges(AsyncReadState state, NativeOverlapped* overlappedPointer, bool useBasicNotifications)
    {
        var informationClass = useBasicNotifications
            ? Kernel32.READ_DIRECTORY_NOTIFY_INFORMATION_CLASS.ReadDirectoryNotifyInformation
            : Kernel32.READ_DIRECTORY_NOTIFY_INFORMATION_CLASS.ReadDirectoryNotifyExtendedInformation;

        return Kernel32.ReadDirectoryChangesExW(
            state.DirectoryHandle,
            state.Buffer, // the buffer is kept pinned for the duration of the sync and async operation by the PreAllocatedOverlapped
            _internalBufferSize,
            _includeSubdirectories,
            (uint)_notifyFilters,
            null,
            overlappedPointer,
            null,
            informationClass);
    }

    /// <summary>
    /// Forgets all remembered IDs, for example after notifications were lost.
    /// </summary>
    private void ForgetKnownObjects()
    {
        lock (_knownObjects)
        {
            _knownObjects.Clear();
        }
    }

    private unsafe void ParseBasicEventBufferAndNotifyForEach(byte[] buffer)
    {
        fixed (byte* b = buffer)
        {
            Kernel32.FILE_NOTIFY_INFORMATION* info = (Kernel32.FILE_NOTIFY_INFORMATION*)b;
            Kernel32.FILE_NOTIFY_INFORMATION* oldInfo = null;

            // A rename without known ID may add one entry without ID
            StartBatchingEvents(NumberOfEntries(info) * 2);

            lock (_knownObjects)
            {
                // Same pairing of FILE_ACTION_RENAMED_OLD_NAME and FILE_ACTION_RENAMED_NEW_NAME as in
                // ParseEventBufferAndNotifyForEach.
                do
                {
                    switch (info->Action)
                    {
                        case Kernel32.FileAction.FILE_ACTION_RENAMED_OLD_NAME:
                            oldInfo = info;
                            break;

                        case Kernel32.FileAction.FILE_ACTION_RENAMED_NEW_NAME:
                            OnRenamed(oldInfo != null ? oldInfo->FileName.ToString() : null, info->FileName.ToString());
                            oldInfo = null;
                            break;

                        default:
                            if (oldInfo != null)
                            {
                                // Previous FILE_ACTION_RENAMED_OLD_NAME with no new name, the object left the watched directory
                                OnMovedOut(oldInfo->FileName.ToString());
                                oldInfo = null;
                            }

                            switch (info->Action)
                            {
                                case Kernel32.FileAction.FILE_ACTION_ADDED:
                                    OnAddedOrModified(WatcherChangeTypes.Created, info->FileName.ToString());
                                    break;
                                case Kernel32.FileAction.FILE_ACTION_REMOVED:
                                    OnRemoved(info->FileName.ToString());
                                    break;
                                case Kernel32.FileAction.FILE_ACTION_MODIFIED:
                                    OnAddedOrModified(WatcherChangeTypes.Changed, info->FileName.ToString());
                                    break;
                            }

                            break;
                    }

                    info = Kernel32.FILE_NOTIFY_INFORMATION.NextInfo(info);
                }
                while (info != null);

                if (oldInfo != null)
                {
                    OnMovedOut(oldInfo->FileName.ToString());
                }
            }

            FinishBatchingEvents();
        }
    }

    private void OnAddedOrModified(WatcherChangeTypes changeType, string path)
    {
        switch (TryLookUp(path, out var current))
        {
            case LookUpResult.Found:
                Remember(path, current);
                NotifyFileSystemEventArgs(changeType, path, current);
                break;

            case LookUpResult.NotFound:
                // The object is no longer at this path. The rename or removal that took it away follows
                // in the notifications and is reported there.
                break;

            default:
                NotifyFileSystemEventArgs(changeType, path, default);
                break;
        }
    }

    private void OnRemoved(string path)
    {
        if (!_knownObjects.TryGetValue(path, out var known))
        {
            NotifyFileSystemEventArgs(WatcherChangeTypes.Deleted, path, default);
            return;
        }

        var result = TryLookUp(path, out var current);
        Forget(path, known);

        if (result is LookUpResult.Failed || (result is LookUpResult.Found && current.FileId == known.FileId))
        {
            // The remembered object is still at this path, or the path cannot be checked. The removal
            // refers to something else, do not attribute it to the remembered object.
            NotifyFileSystemEventArgs(WatcherChangeTypes.Deleted, path, default);
            return;
        }

        NotifyFileSystemEventArgs(WatcherChangeTypes.Deleted, path, known);
    }

    private void OnRenamed(string? oldPath, string newPath)
    {
        var hasKnown = oldPath is not null && _knownObjects.TryGetValue(oldPath, out _);
        var known = hasKnown ? _knownObjects[oldPath!] : default;

        if (TryLookUp(newPath, out var current) is LookUpResult.Found)
        {
            if (hasKnown)
            {
                MoveKnownDescendants(oldPath!, newPath, known);
            }

            Remember(newPath, current);
            NotifyRenameEventArgs(WatcherChangeTypes.Renamed, newPath, oldPath.AsSpan(), current, oldParentFileId: hasKnown ? known.ParentFileId : default);

            if (hasKnown && known.FileId != current.FileId)
            {
                // The object now at the new path is not the one that was renamed. Whatever happened to the
                // remembered one is left to re-enumeration.
                NotifyFileSystemEventArgs(WatcherChangeTypes.Deleted, oldPath, default);
            }

            return;
        }

        // The renamed object is already gone from the new path (renamed again or removed). Report the rename
        // with the remembered ID, the following notifications report what happened next.
        if (hasKnown && TryGetParentId(newPath, out var newParentId))
        {
            var moved = known;
            moved.ParentFileId = newParentId;
            MoveKnownDescendants(oldPath!, newPath, known);
            Remember(newPath, moved);
            NotifyRenameEventArgs(WatcherChangeTypes.Renamed, newPath, oldPath, moved, oldParentFileId: known.ParentFileId);
            return;
        }

        if (hasKnown)
        {
            Forget(oldPath!, known);
        }

        NotifyRenameEventArgs(WatcherChangeTypes.Renamed, newPath, oldPath.AsSpan(), default, oldParentFileId: default);
    }

    private void OnMovedOut(string oldPath)
    {
        if (!_knownObjects.TryGetValue(oldPath, out var known))
        {
            NotifyRenameEventArgs(WatcherChangeTypes.Renamed, ReadOnlySpan<char>.Empty, oldPath, default, oldParentFileId: default);
            return;
        }

        Forget(oldPath, known);

        // Same as the extended notifications: the destination parent is unknown
        var info = known;
        info.ParentFileId = default;
        NotifyRenameEventArgs(WatcherChangeTypes.Renamed, ReadOnlySpan<char>.Empty, oldPath, info, oldParentFileId: known.ParentFileId);
    }

    private void Remember(string path, FileExtendedInfo info)
    {
        if (_knownObjects.Count >= MaxKnownObjects && !_knownObjects.ContainsKey(path))
        {
            // Unbounded growth is not worth it, unknown objects fall back to re-enumeration
            _knownObjects.Clear();
        }

        _knownObjects[path] = info;
    }

    private void Forget(string path, FileExtendedInfo known)
    {
        _knownObjects.Remove(path);

        if ((known.Attributes & FileAttributes.Directory) != 0)
        {
            var prefix = path + System.IO.Path.DirectorySeparatorChar;
            foreach (var key in _knownObjects.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                _knownObjects.Remove(key);
            }
        }
    }

    private void MoveKnownDescendants(string oldPath, string newPath, FileExtendedInfo known)
    {
        _knownObjects.Remove(oldPath);

        if ((known.Attributes & FileAttributes.Directory) == 0)
        {
            return;
        }

        // File IDs survive a directory rename, only the paths of the content change
        var prefix = oldPath + System.IO.Path.DirectorySeparatorChar;
        foreach (var key in _knownObjects.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            var value = _knownObjects[key];
            _knownObjects.Remove(key);
            _knownObjects[newPath + key.Substring(oldPath.Length)] = value;
        }
    }

    private bool TryGetParentId(string path, out long parentId)
    {
        parentId = 0;
        var parentPath = System.IO.Path.GetDirectoryName(path);

        if (string.IsNullOrEmpty(parentPath))
        {
            try
            {
                using var root = FileSystemDirectory.Open(_directory, FileSystemFileAccess.ReadAttributes, FileShare.ReadWrite | FileShare.Delete);
                parentId = root.ObjectId;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }
        else if (TryLookUp(parentPath, out var parent) is LookUpResult.Found)
        {
            parentId = parent.FileId;
        }

        return parentId is not (0 or ReFsFileId.InvalidFileId);
    }

    private unsafe int NumberOfEntries(Kernel32.FILE_NOTIFY_INFORMATION* entry)
    {
        int i;
        for (i = 0; entry != null; i++)
        {
            entry = Kernel32.FILE_NOTIFY_INFORMATION.NextInfo(entry);
        }

        return i;
    }

    /// <summary>
    /// Reads the metadata and IDs of the object at the given path relative to the watched directory.
    /// </summary>
    /// <remarks>
    /// The object itself is never opened. Opening it, even for reading attributes only, races with
    /// multi-step operations of other applications: on ReFS, <c>ReplaceFile</c> ("safe save" of many
    /// editors) then fails with <c>ERROR_UNABLE_TO_REMOVE_REPLACED</c>. The parent directory is queried
    /// for the single entry instead, the same way directory enumeration reports IDs to the sync engine.
    /// </remarks>
    /// <returns>
    /// <see cref="LookUpResult.NotFound"/> only when the object or its parent directory does not exist,
    /// <see cref="LookUpResult.Failed"/> when the lookup failed for another reason or the ID is not usable.
    /// </returns>
    private LookUpResult TryLookUp(string relativePath, out FileExtendedInfo info)
    {
        info = default;

        try
        {
            // Path is a property of the watcher, hence the fully qualified name
            var fullPath = System.IO.Path.Join(_directory, relativePath);
            var parentPath = System.IO.Path.GetDirectoryName(fullPath);
            var name = System.IO.Path.GetFileName(fullPath);

            if (string.IsNullOrEmpty(parentPath) || string.IsNullOrEmpty(name))
            {
                return LookUpResult.Failed;
            }

            using var parentDirectory = FileSystemDirectory.Open(
                parentPath,
                FileSystemFileAccess.ReadData,
                FileShare.ReadWrite | FileShare.Delete);

            var entry = parentDirectory.EnumerateFileSystemEntries(name, LookUpEnumerationOptions, ownsHandle: false).SingleOrDefault();

            if (entry is null)
            {
                return LookUpResult.NotFound;
            }

            var parentFileId = parentDirectory.ObjectId;

            if (entry.ObjectId is 0 or ReFsFileId.InvalidFileId || parentFileId is 0 or ReFsFileId.InvalidFileId)
            {
                return LookUpResult.Failed;
            }

            var isDirectory = (entry.Attributes & FileAttributes.Directory) != 0;

            info = new FileExtendedInfo
            {
                CreationTimeUtc = entry.CreationTimeUtc,
                LastModificationTimeUtc = entry.LastWriteTimeUtc,
                LastChangeTimeUtc = entry.LastWriteTimeUtc,
                FileSize = isDirectory ? 0 : entry.Size,
                Attributes = entry.Attributes,
                FileId = entry.ObjectId,
                ParentFileId = parentFileId,
                ReparseTag = entry.ReparseTag,
            };

            return LookUpResult.Found;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return LookUpResult.NotFound;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return LookUpResult.Failed;
        }
    }
}
