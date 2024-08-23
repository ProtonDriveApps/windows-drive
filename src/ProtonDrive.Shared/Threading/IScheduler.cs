using System;
using System.Threading.Tasks;

namespace ProtonDrive.Shared.Threading;

public interface IScheduler
{
    Task<T> Schedule<T>(Func<Task<T>> function);

    ISchedulerTimer CreateTimer();
}
