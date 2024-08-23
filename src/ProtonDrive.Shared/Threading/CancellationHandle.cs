using System.Threading;

namespace ProtonDrive.Shared.Threading;

public class CancellationHandle
{
    private volatile CancellationTokenSource _tokenSource = new();

    public CancellationToken Token => _tokenSource.Token;

    public void Cancel()
    {
        var newSource = new CancellationTokenSource();
        while (true)
        {
            var source = _tokenSource;
            var prevSource = Interlocked.CompareExchange(ref _tokenSource, newSource, source);
            if (prevSource != source)
            {
                continue;
            }

            prevSource.Cancel();
            prevSource.Dispose();
            return;
        }
    }
}
