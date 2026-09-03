namespace Proton.Drive.Shared;

public interface IClock
{
    TickCount TickCount { get; }
    DateTime UtcNow { get; }

    /// <summary>
    /// Creates a cancellable task that completes after a specified time interval.
    /// </summary>
    /// <param name="delay">The time span to wait before completing the returned task, or <see cref="Timeout.InfiniteTimeSpan"/> to wait indefinitely.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task that represents the time delay.</returns>
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}
