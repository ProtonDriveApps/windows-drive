using Proton.Drive.Sdk.Sync.Agent.Settings;

namespace Proton.Drive.Sdk.Sync.Agent.Mapping;

internal interface ILocalSyncFolderValidator
{
    SyncFolderValidationResult? ValidateDrive(string path, SyncMethod syncMethod);
    SyncFolderValidationResult? ValidatePath(string path, IReadOnlySet<string> otherPaths);
    SyncFolderValidationResult? ValidateFolder(string path, bool shouldBeEmpty);
}
