namespace Proton.Drive.Sdk.Sync.Shared;

public interface ISyncService
{
    bool SyncIsPaused { get; }

    /// <summary>
    /// Resumes paused synchronization.
    /// </summary>
    void Resume();

    /// <summary>
    /// Pauses synchronization until the specified moment in time. If <paramref name="time"/> is <see langword="null"/>,
    /// synchronization remains paused indefinitely until manually resumed.
    /// </summary>
    /// <param name="time">
    /// The moment in time at which synchronization should resume, or <see langword="null"/> to pause indefinitely.
    /// <para>
    /// If <see cref="DateTime"/> value is of <see cref="DateTimeKind.Utc"/> kind, sync resumption happens at the specified moment in time.
    /// If value is of <see cref="DateTimeKind.Local"/> kind, sync resumption happens at the specified date, hour and minute in local time,
    /// despite local time zone changes after being paused.
    /// </para>
    /// </param>
    void PauseUntil(DateTime? time);

    Task RestartAsync();
}
