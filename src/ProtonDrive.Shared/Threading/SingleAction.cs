using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Shared.Threading;

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

    public Task CurrentTask => _origin.CurrentTask;

    public Task RunAsync() => _origin.RunAsync(CancellationToken.None);

    public void Cancel() => _origin.Cancel();

    private struct Void { }
}
