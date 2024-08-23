using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using ProtonDrive.App.Settings;

namespace ProtonDrive.App.Mapping;

internal static class MappingExtensions
{
    /// <summary>
    /// Obtains account root folder path from the cloud files mapping.
    /// Account root folder is a parent folder of the cloud files folder ("My files").
    /// </summary>
    /// <param name="mappings">The sequence of mappings to obtain account root folder path from.</param>
    /// <param name="path">Returns account root folder path, if the active cloud files mapping is available with local root folder path specified.</param>
    /// <returns>True, if the account root folder path was successfully obtained; False otherwise.</returns>
    public static bool TryGetAccountRootFolderPath(this IEnumerable<RemoteToLocalMapping> mappings, [MaybeNullWhen(false)] out string path)
    {
        path = null;

        var cloudFilesMapping = mappings.FirstOrDefault(m => m.Type is MappingType.CloudFiles);

        return cloudFilesMapping?.TryGetAccountRootFolderPath(out path) == true;
    }

    /// <summary>
    /// Attempts to obtain account root folder path from the cloud files mapping.
    /// Account root folder is a parent folder of the cloud files folder ("My files").
    /// </summary>
    /// <param name="mapping">The mapping to obtain account root folder path from.</param>
    /// <param name="path">Returns account root folder path, if the mapping is active cloud files mapping with local root folder path specified.</param>
    /// <returns>True, if the account root folder path was successfully obtained; False otherwise.</returns>
    public static bool TryGetAccountRootFolderPath(this RemoteToLocalMapping mapping, [MaybeNullWhen(false)] out string path)
    {
        path = null;

        if (mapping.Type is not MappingType.CloudFiles)
        {
            return false;
        }

        if (mapping.Status is not MappingStatus.New and not MappingStatus.Complete and not MappingStatus.Deleted)
        {
            return false;
        }

        path = Path.GetDirectoryName(mapping.Local.RootFolderPath);

        return !string.IsNullOrEmpty(path);
    }
}
