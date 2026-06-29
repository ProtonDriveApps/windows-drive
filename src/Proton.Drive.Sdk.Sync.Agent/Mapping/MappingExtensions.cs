using System.Diagnostics.CodeAnalysis;
using Proton.Drive.Sdk.Sync.Agent.Settings;
using Proton.Drive.Sdk.Sync.Shared;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping;

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

        path = Path.GetDirectoryName(mapping.Local.Path);

        return !string.IsNullOrEmpty(path);
    }

    public static bool IsSetUp(this RemoteReplica replica)
    {
        // VolumeId was missing in earlier versions. For backward compatibility, we do not check it.
        return !string.IsNullOrEmpty(replica.ShareId)
            && !string.IsNullOrEmpty(replica.RootLinkId);
    }

    public static bool IsSetUp(this LocalReplica replica)
    {
        return replica.RootFolderId != 0;
    }

    /// <summary>
    /// Checks whether the folder mapping type is defined.
    /// Previously, more types of folder mappings were used, that might be read from the settings. Mappings with undefined type can be safely ignored.
    /// </summary>
    /// <param name="mapping">Sync folder mapping.</param>
    /// <returns>True if folder mapping type is defined; false otherwise.</returns>
    public static bool TypeIsDefined(this RemoteToLocalMapping mapping)
    {
        return Enum.IsDefined(mapping.Type);
    }
}
