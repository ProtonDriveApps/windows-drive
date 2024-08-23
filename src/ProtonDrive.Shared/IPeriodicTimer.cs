using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProtonDrive.Shared;

public interface IPeriodicTimer : IDisposable
{
    ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken = default);
}
