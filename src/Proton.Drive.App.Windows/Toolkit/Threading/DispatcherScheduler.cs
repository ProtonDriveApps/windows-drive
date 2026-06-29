using System.Windows.Threading;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.App.Windows.Toolkit.Threading;

internal class DispatcherScheduler : IScheduler
{
    private readonly Dispatcher _dispatcher;

    internal DispatcherScheduler(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void Schedule(Action action)
    {
        _dispatcher.InvokeAsync(action, DispatcherPriority.Background);
    }

    public async Task ScheduleAsync(Action action)
    {
        await _dispatcher.InvokeAsync(action, DispatcherPriority.Background);
    }

    public async Task<T> ScheduleAsync<T>(Func<T> action)
    {
        return await _dispatcher.InvokeAsync(action, DispatcherPriority.Background);
    }

    public async Task<T> Schedule<T>(Func<Task<T>> function)
    {
        return await (await _dispatcher.InvokeAsync(function, DispatcherPriority.Background).Task.ConfigureAwait(false)).ConfigureAwait(false);
    }

    public ISchedulerTimer CreateTimer()
    {
        return new DispatcherTimer(_dispatcher);
    }
}
