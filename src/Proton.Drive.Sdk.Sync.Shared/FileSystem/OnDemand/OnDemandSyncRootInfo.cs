using Proton.Drive.Sdk.Sync.Shared.Shell;

namespace Proton.Drive.Sdk.Sync.Shared.FileSystem.OnDemand;

public record OnDemandSyncRootInfo(string Path, string RootId, ShellFolderVisibility Visibility, ShellFolderSiblingsGrouping SiblingsGrouping);
