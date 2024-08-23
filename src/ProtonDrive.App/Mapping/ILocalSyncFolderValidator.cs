using System.Collections.Generic;

namespace ProtonDrive.App.Mapping;

internal interface ILocalSyncFolderValidator
{
    SyncFolderValidationResult ValidatePath(string path, IReadOnlySet<string> otherPaths);

    SyncFolderValidationResult ValidatePathAndDrive(string path, IReadOnlySet<string> otherPaths);

    SyncFolderValidationResult ValidateFolder(string path, bool shouldBeEmpty);
}
