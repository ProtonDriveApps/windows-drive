namespace Proton.Drive.Sdk.Sync.Shared.Health;

public sealed record FileConsistencyGuardFileStatisticsEntry
{
    public required int NumberOfFiles { get; init; }
    public required int NumberOfBlocks { get; init; }
    public required double TotalFileSizeInGigaBytes { get; init; }
    public required FileConsistencyGuardFileStatus Status { get; init; }
    public required FileConsistencyGuardFileReason Reason { get; init; }
    public FileConsistencyGuardFileReason DownloadReason { get; init; }
    public required FileConsistencyGuardFileError Error { get; init; }
}
