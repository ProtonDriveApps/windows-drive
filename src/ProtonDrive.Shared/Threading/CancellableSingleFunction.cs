using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Shared.Threading;

public class CancellableSingleFunction<T> : SingleFunction<T>
{
    private readonly CancellationHandle _cancellationHandle = new();

    public CancellableSingleFunction(Func<T?> function)
        : base(function)
    {
    }

    public CancellableSingleFunction(Func<Task<T?>> function)
        : base(function)
    {
    }

    public CancellableSingleFunction(Func<CancellationToken, Task<T?>> function)
        : base(function)
    {
    }

    public override async Task<T?> RunAsync(CancellationToken cancellationToken)
    {
        using var cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationHandle.Token);

        return await base.RunAsync(cancellationSource.Token).ConfigureAwait(false);
    }

    public void Cancel() => _cancellationHandle.Cancel();
}
