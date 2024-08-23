using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Client;

internal sealed class BlockingArrayMemoryPool<T> : MemoryPool<T>
{
    public BlockingArrayMemoryPool(int maxArrayLength, int maxArraysPerBucket)
    {
        MaxBufferSize = maxArrayLength;
        Semaphore = new SemaphoreSlim(maxArraysPerBucket, maxArraysPerBucket);
        ArrayPool = ArrayPool<T>.Create(maxArrayLength, maxArraysPerBucket);
    }

    public override int MaxBufferSize { get; }

    private ArrayPool<T> ArrayPool { get; }
    private SemaphoreSlim Semaphore { get; }

    public override IMemoryOwner<T> Rent(int minBufferSize = -1)
    {
        var bufferSize = GetBufferSize(minBufferSize);

        Semaphore.Wait(CancellationToken.None);
        var buffer = ArrayPool.Rent(bufferSize);

        return new MemoryOwner(buffer, this);
    }

    public Task<IMemoryOwner<T>> RentAsync(int minBufferSize, CancellationToken cancellationToken)
    {
        var bufferSize = GetBufferSize(minBufferSize);

        return FinishRentAsync();

        // Splitting async/await into another method for fail-fast on argument validation
        // (otherwise those exceptions will only propagate when the returned task is awaited)
        async Task<IMemoryOwner<T>> FinishRentAsync()
        {
            await Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            var buffer = ArrayPool.Rent(bufferSize);

            return new MemoryOwner(buffer, this);
        }
    }

    public Task<IMemoryOwner<T>> RentAsync(CancellationToken cancellationToken)
    {
        return RentAsync(-1, cancellationToken);
    }

    protected override void Dispose(bool disposing) { /* Nothing to do */ }

    private int GetBufferSize(int minBufferSize)
    {
        if (minBufferSize == -1)
        {
            return MaxBufferSize;
        }

        if ((uint)minBufferSize > MaxBufferSize)
        {
            throw new ArgumentOutOfRangeException(nameof(minBufferSize));
        }

        return minBufferSize;
    }

    private sealed class MemoryOwner : IMemoryOwner<T>
    {
        private readonly BlockingArrayMemoryPool<T> _pool;
        private readonly T[] _array;

        private bool _isDisposed;

        public MemoryOwner(T[] array, BlockingArrayMemoryPool<T> pool)
        {
            _array = array;
            _pool = pool;
        }

        public Memory<T> Memory => _array;

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _pool.ArrayPool.Return(_array);
            _pool.Semaphore.Release();
            _isDisposed = true;
        }
    }
}
