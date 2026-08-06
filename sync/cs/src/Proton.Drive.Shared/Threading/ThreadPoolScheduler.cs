namespace Proton.Drive.Shared.Threading;

public class ThreadPoolScheduler : IScheduler
{
    public Task<T> Schedule<T>(Func<Task<T>> function)
    {
        throw new NotSupportedException();
    }

    public ISchedulerTimer CreateTimer()
    {
        return new ThreadPoolTimer();
    }
}
