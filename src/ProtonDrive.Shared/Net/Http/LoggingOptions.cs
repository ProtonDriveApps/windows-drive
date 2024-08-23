using System.Net.Http;
using System.Threading;

namespace ProtonDrive.Shared.Net.Http;

internal class LoggingOptions
{
    private static int _lastRequestId;

    public static HttpRequestOptionsKey<LoggingOptions> Key { get; } = new(nameof(LoggingOptions));

    public int RequestId { get; } = GetRequestId();
    public int AttemptNumber { get; private set; }

    public void AddAttempt()
    {
        AttemptNumber++;
    }

    private static int GetRequestId()
    {
        return Interlocked.Increment(ref _lastRequestId);
    }
}
