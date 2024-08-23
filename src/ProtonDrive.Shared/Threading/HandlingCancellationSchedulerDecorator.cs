using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ProtonDrive.Shared.Threading;

public sealed class HandlingCancellationSchedulerDecorator : IScheduler
{
    private readonly string _subjectName;
    private readonly ILogger _logger;
    private readonly IScheduler _decoratedInstance;

    public HandlingCancellationSchedulerDecorator(string subjectName, ILogger logger, IScheduler decoratedInstance)
    {
        _subjectName = subjectName;
        _logger = logger;
        _decoratedInstance = decoratedInstance;
    }

    [DebuggerHidden]
    [DebuggerStepThrough]
    public Task<T> Schedule<T>(Func<Task<T>> function)
    {
        return _decoratedInstance.Schedule(() => InternalFunction(function));
    }

    public ISchedulerTimer CreateTimer()
    {
        return _decoratedInstance.CreateTimer();
    }

    private async Task<T> InternalFunction<T>(Func<Task<T>> function)
    {
        try
        {
            return await function().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("{SubjectName} operation was cancelled", _subjectName);

            return default!;
        }
    }
}
