using ProtonDrive.Sync.Shared.Shell;

namespace ProtonDrive.Sync.Shared.FileSystem.OnDemand;

public record OnDemandSyncRootInfo(string Path, string RootId, ShellFolderVisibility Visibility, ShellFolderSiblingsGrouping SiblingsGrouping);
