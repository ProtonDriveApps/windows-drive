using Proton.Drive.Sdk.Sync.Shared.FileSystem;
using Proton.Drive.Sdk.Sync.Shared.Trees.FileSystem;
using Proton.Drive.Shared.IO;

namespace Proton.Drive.Sdk.Sync.Shared.SyncActivity;

public sealed record SyncActivityItem<TId>
{
    public TId? Id { get; init; }
    public int? RootId { get; init; }
    public Replica Replica { get; init; }
    public SyncActivitySource Source { get; init; }
    public SyncActivityItemStatus Status { get; init; }
    public NodeType NodeType { get; init; }
    public SyncActivityType ActivityType { get; init; }
    public string Name { get; init; } = string.Empty;
    public string RelativeParentFolderPath { get; init; } = string.Empty;
    public string LocalRootPath { get; init; } = string.Empty;
    public long? Size { get; init; }
    public SyncActivityStage Stage { get; init; }
    public FileSystemErrorCode ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public Progress Progress { get; init; } = Progress.Zero;
}
