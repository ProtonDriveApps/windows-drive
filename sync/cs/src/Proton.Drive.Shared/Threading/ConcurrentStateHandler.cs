namespace Proton.Drive.Shared.Threading;

public sealed class ConcurrentStateHandler
{
    private readonly StateChangeHandler _stateChangeHandler;

    private Status _status;

    public ConcurrentStateHandler(StateChangeHandler stateChangeHandler)
    {
        _stateChangeHandler = stateChangeHandler;
    }

    public delegate void StateChangeHandler(Status fromStatus, Status toStatus);

    public enum Status
    {
        NotRequested,
        Requested,
        Completed,
    }

    public bool IsNotRequested => _status == Status.NotRequested;
    public bool IsRequested => _status == Status.Requested;
    public bool IsCompleted => _status == Status.Completed;

    public bool TryStart() => TryChangeStatus(Status.NotRequested, Status.Requested);
    public bool TryComplete() => TryChangeStatus(Status.Requested, Status.Completed);
    public bool TryRestart() => TryChangeStatus(Status.Completed, Status.Requested);

    public bool TryCancel()
    {
        const Status toStatus = Status.NotRequested;
        var previousStatus = Interlocked.Exchange(ref _status, toStatus);

        if (previousStatus is toStatus)
        {
            return false;
        }

        _stateChangeHandler.Invoke(previousStatus, toStatus);
        return true;
    }

    private bool TryChangeStatus(Status fromStatus, Status toStatus)
    {
        var previousStatus = Interlocked.CompareExchange(ref _status, toStatus, fromStatus);

        if (previousStatus != fromStatus)
        {
            return false;
        }

        _stateChangeHandler.Invoke(fromStatus, toStatus);
        return true;
    }
}
