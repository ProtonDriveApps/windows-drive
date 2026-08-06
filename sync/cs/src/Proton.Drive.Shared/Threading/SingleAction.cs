namespace Proton.Drive.Shared.Threading;

public class SingleAction
{
    private readonly CancellableSingleFunction<Void> _origin;

    public SingleAction(Action action)
        : this(_ =>
        {
            action();
            return Task.CompletedTask;
        })
    { }

    public SingleAction(Func<Task> action)
        : this(_ => action())
    { }

    public SingleAction(Func<CancellationToken, Task> action)
    {
        _origin = new CancellableSingleFunction<Void>(
            async ct =>
            {
                await action(ct).ConfigureAwait(false);

                return default;
            });
    }

    public Task RunAsync() => _origin.RunAsync(CancellationToken.None);

    public void Cancel() => _origin.Cancel();

    /// <summary>
    /// Waits for processing current work to be completed.
    /// </summary>
    /// <returns>A <see cref="Task"/> that completes successfully when processing current work completes.</returns>
    public Task WaitForCompletionAsync()
    {
        return _origin.WaitForCompletionAsync();
    }

    private struct Void;
}
