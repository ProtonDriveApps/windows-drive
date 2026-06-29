namespace Proton.Drive.Shared;

public readonly struct AsyncDisposable : IAsyncDisposable
{
    private readonly IEnumerable<Func<Task>> _disposalActions;

    public AsyncDisposable(params Func<Task>[] disposalActions)
        : this((IEnumerable<Func<Task>>)disposalActions)
    {
    }

    public AsyncDisposable(IEnumerable<Func<Task>> disposalActions)
    {
        _disposalActions = disposalActions;
    }

    public static IAsyncDisposable Empty => Create();

    public static AsyncDisposable Create(params Func<Task>[] disposalActions)
    {
        return new AsyncDisposable(disposalActions);
    }

    public static AsyncDisposable Create(IEnumerable<Func<Task>> disposalActions)
    {
        return new AsyncDisposable(disposalActions);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var action in _disposalActions)
        {
            await action.Invoke().ConfigureAwait(false);
        }
    }
}
