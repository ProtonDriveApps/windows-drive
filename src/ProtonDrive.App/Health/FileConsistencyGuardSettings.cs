using ProtonDrive.Sync.Shared.Health;

namespace ProtonDrive.App.Health;

internal sealed class FileConsistencyGuardSettings
{
    public int Version { get; set; }
    public FileConsistencyGuardStatus Status { get; set; }
    public string? ClientInstanceId { get; set; }
    public IReadOnlyCollection<FileConsistencyGuardFileStatisticsEntry> FileStatistics { get; set; } = [];
}
