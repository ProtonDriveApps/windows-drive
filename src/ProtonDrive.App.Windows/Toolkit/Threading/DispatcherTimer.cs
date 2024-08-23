using System;
using System.Windows.Threading;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.App.Windows.Toolkit.Threading;

internal class DispatcherTimer : ISchedulerTimer
{
    private System.Windows.Threading.DispatcherTimer? _timer;

    public DispatcherTimer(Dispatcher dispatcher)
    {
        _timer = new System.Windows.Threading.DispatcherTimer(DispatcherPriority.Normal, dispatcher);
        _timer.Tick += OnTimerTick;
    }

    public event EventHandler? Tick;

    public TimeSpan Interval
    {
        get => _timer!.Interval;
        set => _timer!.Interval = value;
    }

    public bool IsEnabled
    {
        get => _timer!.IsEnabled;
        set => _timer!.IsEnabled = value;
    }

    public void Start() => _timer!.Start();

    public void Stop() => _timer!.Stop();

    public void Dispose()
    {
        if (_timer != null)
        {
            _timer.Stop();
            _timer.Tick -= OnTimerTick;
            _timer = null;
        }

        Tick = null;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_timer?.IsEnabled == true)
        {
            Tick?.Invoke(sender, e);
        }
    }
}
