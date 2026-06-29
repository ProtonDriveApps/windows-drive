namespace Proton.Drive.Sdk.Sync.Shared.Health;

public sealed record FileConsistencyGuardFileModel
{
    public required long Id { get; init; }
    public required int LocalRootId { get; set; }
    public required int RemoteRootId { get; set; }
    public required long LocalId { get; init; }
    public required string RemoteId { get; set; }
    public required string Name { get; init; }
    public required long LocalSize { get; init; }
    public required long RemoteSize { get; set; }
    public long? RemotePlainSize { get; set; }
    public long? RemoteSizeOnStorage { get; set; }
    public required DateTime LocalLastWriteTime { get; init; }
    public required DateTime RemoteLastWriteTime { get; set; }
    public required string? RevisionId { get; set; }
    public required long ContentVersion { get; init; }
    public string? LocalHash { get; set; }
    public string? RemoteHash { get; set; }
    public long? TrailingZeroBytesLength { get; set; }
    public FileConsistencyGuardFileStatus Status { get; set; }
    public FileConsistencyGuardFileReason Reason { get; set; }
    public FileConsistencyGuardFileReason DownloadReason { get; set; }
    public FileConsistencyGuardFileError Error { get; set; }
}
