using System;

namespace ProtonDrive.Shared.Threading;

public interface ISchedulerTimer : IDisposable
{
    /// <summary>
    /// Occurs when the interval elapses.
    /// </summary>
    /// <remarks>
    /// If processing of the <see cref="Tick"/> event lasts longer than <see cref="Interval"/>,
    /// the next event will be skipped or will be postponed. It is not required
    /// the event handler to be re-entrant.
    /// </remarks>
    event EventHandler Tick;

    TimeSpan Interval { get; set; }
    bool IsEnabled { get; set; }

    void Start();
    void Stop();
}
