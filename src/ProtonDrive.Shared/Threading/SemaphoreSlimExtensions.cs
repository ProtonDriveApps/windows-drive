using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Shared.Threading;

public static class SemaphoreSlimExtensions
{
    public static async Task<IDisposable> LockAsync(this SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new DisposableLock(semaphore);
    }

    private class DisposableLock : IDisposable
    {
        private SemaphoreSlim? _semaphore;

        public DisposableLock(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            _semaphore?.Release();
            _semaphore = null;
        }
    }
}
