using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProtonDrive.Shared.IO;

namespace ProtonDrive.Sync.Adapter.UpdateDetection;

internal sealed class ItemExclusionFilter : IItemExclusionFilter
{
    private readonly IReadOnlyCollection<string> _specialFolderNames;
    private readonly IReadOnlySet<string> _fileExtensionsToIgnore = new[]
    {
        ".crdownload",
        ".download",
        ".partial",
        ".part",
        ".temp",
        ".tmp",
        ".~tmp",
    }.ToHashSet(StringComparer.OrdinalIgnoreCase);

    public ItemExclusionFilter(IReadOnlyCollection<string> specialFolderNames)
    {
        _specialFolderNames = specialFolderNames;
    }

    public bool ShouldBeIgnored(string name, FileAttributes attributes, PlaceholderState placeholderState, bool parentIsSyncRoot)
    {
        return ShouldBeIgnored(name, attributes, placeholderState)
               || (parentIsSyncRoot && ShouldBeIgnoredOnSyncRoot())
            ;

        bool ShouldBeIgnoredOnSyncRoot()
        {
            // Special folders on the replica root are ignored
            return _specialFolderNames.Contains(name, StringComparer.OrdinalIgnoreCase);
        }
    }

    private bool ShouldBeIgnored(string name, FileAttributes attributes, PlaceholderState placeholderState)
    {
        return placeholderState.HasFlag(PlaceholderState.Invalid)
               || attributes.HasFlag(FileAttributes.Device)
               || (attributes.HasFlag(FileAttributes.ReparsePoint) && !placeholderState.HasFlag(PlaceholderState.Placeholder))
               || attributes.HasFlag(FileAttributes.Temporary)
               || IsSystemFile()
               || IsProtectedSystemFolder()
               || IsMicrosoftOrLibreOfficeTemporaryFile()
               || IsWellKnownTemporaryFile()
            ;

        bool IsSystemFile()
        {
            return attributes.HasFlag(FileAttributes.System) && !attributes.HasFlag(FileAttributes.Directory);
        }

        bool IsProtectedSystemFolder()
        {
            return attributes.HasFlag(FileAttributes.System) && attributes.HasFlag(FileAttributes.Hidden) && attributes.HasFlag(FileAttributes.Directory);
        }

        bool IsMicrosoftOrLibreOfficeTemporaryFile()
        {
            return !attributes.HasFlag(FileAttributes.Directory)
                   && // Used by Microsoft Office
                   ((name.StartsWith("~", StringComparison.Ordinal) && name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) ||
                    name.StartsWith("~$", StringComparison.Ordinal)
                    || // Used by Libre Office
                    name.StartsWith(".~", StringComparison.Ordinal));
        }

        bool IsWellKnownTemporaryFile()
        {
            return !attributes.HasFlag(FileAttributes.Directory)
                   && _fileExtensionsToIgnore.Contains(Path.GetExtension(name));
        }
    }
}
