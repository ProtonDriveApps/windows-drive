using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace ProtonDrive.App.FileSystem.Remote;

public sealed class StorageReservationHandler
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private long _reservedByteCount;

    public bool TryReserve(long byteCount, long availableSpace, [MaybeNullWhen(false)] out IDisposable reservation)
    {
        _semaphore.Wait();

        try
        {
            var newReservedByteCount = _reservedByteCount + byteCount;

            if (newReservedByteCount > availableSpace)
            {
                reservation = null;
                return false;
            }

            _reservedByteCount = newReservedByteCount;

            reservation = new StorageReservation(byteCount, this);
            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void ReleaseReservation(long byteCount)
    {
        _semaphore.Wait();

        try
        {
            _reservedByteCount -= byteCount;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private sealed class StorageReservation : IDisposable
    {
        private readonly long _reservedByteCount;
        private readonly StorageReservationHandler _owner;

        public StorageReservation(long reservedByteCount, StorageReservationHandler owner)
        {
            _reservedByteCount = reservedByteCount;
            _owner = owner;
        }

        public void Dispose()
        {
            _owner.ReleaseReservation(_reservedByteCount);
        }
    }
}
