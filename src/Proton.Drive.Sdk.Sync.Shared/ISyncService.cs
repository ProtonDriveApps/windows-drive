using Proton.Drive.Sdk.Sync.Shared.SyncActivity;

namespace Proton.Drive.Sdk.Sync.Shared;

public interface ISyncService
{
    bool SyncIsPaused { get; }

    SyncStatus Status { get; }

    void Resume();

    /// <summary>
    /// Pauses synchronization until the specified resumeAt. If <paramref name="resumeAt"/> is <see langword="null"/>,
    /// synchronization remains paused indefinitely until resumed.
    /// </summary>
    /// <param name="resumeAt">
    /// The resumeAt at which synchronization should resume, or <see langword="null"/> to pause indefinitely.
    /// </param>
    void PauseUntil(DateTimeOffset? resumeAt);

    Task RestartAsync();
}
