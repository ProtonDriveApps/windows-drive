using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Shared.Threading;

public static class TaskCompletionSourceExtensions
{
    public static async Task Wrap(this TaskCompletionSource source, Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
            source.SetResult();
        }
        catch (OperationCanceledException)
        {
            source.SetCanceled();
        }
        catch (Exception e)
        {
            source.SetException(e);
        }
    }

    public static async Task Wrap<T>(this TaskCompletionSource<T> source, Func<Task<T>> function)
    {
        try
        {
            source.SetResult(await function().ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            source.SetCanceled();
        }
        catch (Exception e)
        {
            source.SetException(e);
        }
    }

    public static TaskCompletionSource<T> WithCancellation<T>(this TaskCompletionSource<T> source, CancellationToken cancellationToken)
    {
        var cancellationRegistration = cancellationToken.Register(() => source.TrySetCanceled());

        source.Task.ContinueWith(_ => cancellationRegistration.Unregister(), TaskContinuationOptions.ExecuteSynchronously);

        return source;
    }
}
