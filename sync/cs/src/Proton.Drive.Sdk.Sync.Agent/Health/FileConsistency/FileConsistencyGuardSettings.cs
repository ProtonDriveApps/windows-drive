using Proton.Drive.Sdk.Sync.Shared.Health;

namespace Proton.Drive.Sdk.Sync.Agent.Health.FileConsistency;

internal sealed class FileConsistencyGuardSettings
{
    public int Version { get; set; }
    public FileConsistencyGuardStatus Status { get; set; }
    public string? ClientInstanceId { get; set; }
    public IReadOnlyCollection<FileConsistencyGuardFileStatisticsEntry> FileStatistics { get; set; } = [];
}
