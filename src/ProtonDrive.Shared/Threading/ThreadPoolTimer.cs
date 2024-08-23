using System;
using System.Threading;
using System.Timers;
using Timer = System.Timers.Timer;

namespace ProtonDrive.Shared.Threading;

internal class ThreadPoolTimer : ISchedulerTimer
{
    private readonly Timer _timer;

    private bool _disposed;
    private volatile bool _enabled;
    private int _tickEntryCount;

    public ThreadPoolTimer()
    {
        _timer = new Timer();
        _timer.Elapsed += OnTimerElapsed;
    }

    public event EventHandler? Tick;

    public TimeSpan Interval
    {
        get => TimeSpan.FromMilliseconds(_timer.Interval);
        set
        {
            _timer.Interval = value.TotalMilliseconds;
            _timer.AutoReset = true;
        }
    }

    public bool IsEnabled
    {
        get => _enabled;
        set
        {
            if (value == _enabled)
            {
                return;
            }

            if (value)
            {
                Start();
            }
            else
            {
                Stop();
            }
        }
    }

    public void Start()
    {
        _enabled = true;
        _timer.Start();
    }

    public void Stop()
    {
        _enabled = false;
        _timer.Stop();
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            Stop();
            Tick = null;
            _timer.Dispose();
        }

        _disposed = true;
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        /* If processing of the event lasts longer than <see cref="Interval"/>,
        // the event might be raised again on another ThreadPool thread. In this situation,
        // the event handler should be re-entrant.*/

        // Prevent reentrancy by skipping next events while the previous is not yet handled
        if (!_enabled || Interlocked.CompareExchange(ref _tickEntryCount, 1, 0) != 0)
        {
            return;
        }

        try
        {
            Tick?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            Interlocked.Exchange(ref _tickEntryCount, 0);
        }
    }
}
