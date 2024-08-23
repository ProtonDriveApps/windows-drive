using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Client;

internal sealed class TransientMemoryPool<T>
{
    private readonly BlockingArrayMemoryPool<T> _origin;

    private readonly CancellationTokenSource _rentCancellation = new();

    public TransientMemoryPool(BlockingArrayMemoryPool<T> origin)
    {
        _origin = origin;
    }

    public async Task<IMemoryOwner<T>> RentAsync(CancellationToken cancellationToken)
    {
        var memory = await _origin.RentAsync(cancellationToken).ConfigureAwait(false);

        return new MemoryOwner(memory, _rentCancellation.Token);
    }

    public async Task<IMemoryOwner<T>> RentAsync(int minBufferSize, CancellationToken cancellationToken)
    {
        var memory = await _origin.RentAsync(minBufferSize, cancellationToken).ConfigureAwait(false);

        return new MemoryOwner(memory, _rentCancellation.Token);
    }

    /// <summary>
    /// Returns all memory buffers rented using this <see cref="TransientMemoryPool{T}"/> instance.
    /// </summary>
    public void Dispose()
    {
        _rentCancellation.Cancel();
    }

    private sealed class MemoryOwner : IMemoryOwner<T>
    {
        private readonly IMemoryOwner<T> _origin;
        private readonly IDisposable _tokenRegistration;

        public MemoryOwner(IMemoryOwner<T> origin, CancellationToken cancellationToken)
        {
            _origin = origin;
            _tokenRegistration = cancellationToken.Register(Dispose);
        }

        public Memory<T> Memory => _origin.Memory;

        public void Dispose()
        {
            _tokenRegistration.Dispose();
            _origin.Dispose();
        }
    }
}
