namespace Proton.Drive.Shared.Threading;

public interface IScheduler
{
    Task<T> Schedule<T>(Func<Task<T>> function);

    ISchedulerTimer CreateTimer();
}
