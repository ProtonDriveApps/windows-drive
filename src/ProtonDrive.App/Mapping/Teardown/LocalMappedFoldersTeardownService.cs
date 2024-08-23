using System;
using System.IO;
using ProtonDrive.App.Configuration;
using ProtonDrive.App.Settings;
using ProtonDrive.App.SystemIntegration;
using ProtonDrive.Client.Contracts;
using ProtonDrive.Shared.Repository;
using ProtonDrive.Shared.Text.Serialization;

namespace ProtonDrive.App.Mapping.Teardown;

public sealed class LocalMappedFoldersTeardownService
{
    public static bool TryUnprotectLocalFolders(ISyncFolderStructureProtector syncFolderStructureProtector)
    {
        try
        {
            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var mappingsJsonPath = Path.Combine(
                localAppDataPath,
                AppRuntimeConfigurationSource.ProtonFolderName,
                AppRuntimeConfigurationSource.ProtonDriveFolderName,
                AppRuntimeConfigurationSource.SyncFoldersMappingFilename);

            var mappingRepository = GetRepository(mappingsJsonPath);

            var mappingSettings = mappingRepository.Get();

            if (mappingSettings is null)
            {
                return true;
            }

            var mappingsDeleted = false;

            foreach (var mapping in mappingSettings.Mappings)
            {
                if (mapping.Status is MappingStatus.TornDown)
                {
                    continue;
                }

                if (mapping.Status is not MappingStatus.Deleted)
                {
                    mapping.Status = MappingStatus.Deleted;
                    mappingsDeleted = true;
                }

                var syncFolderPath = mapping.Local.RootFolderPath;
                var parentFolderPath = Path.GetDirectoryName(syncFolderPath) ?? string.Empty;

                switch (mapping.Type)
                {
                    case MappingType.CloudFiles or MappingType.ForeignDevice:
                        syncFolderStructureProtector.Unprotect(syncFolderPath, FolderProtectionType.Leaf);
                        syncFolderStructureProtector.Unprotect(parentFolderPath, FolderProtectionType.Ancestor);
                        break;

                    case MappingType.SharedWithMeItem when mapping.Remote.RootLinkType is LinkType.Folder:
                        var sharedWithMeItemsFolderPath = Path.GetDirectoryName(mapping.Local.RootFolderPath);

                        if (sharedWithMeItemsFolderPath is not null)
                        {
                            syncFolderStructureProtector.Unprotect(sharedWithMeItemsFolderPath, FolderProtectionType.AncestorWithFiles);
                        }

                        syncFolderStructureProtector.Unprotect(mapping.Local.RootFolderPath, FolderProtectionType.Leaf);
                        break;
                }
            }

            if (mappingsDeleted)
            {
                mappingRepository.Set(mappingSettings);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IRepository<MappingSettings> GetRepository(string filePath)
    {
        return new SafeRepository<MappingSettings>(
            new FileRepository<MappingSettings>(
                new JsonUtf8Serializer(),
                filePath));
    }
}
