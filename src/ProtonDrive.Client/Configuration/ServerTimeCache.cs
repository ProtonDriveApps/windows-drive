using System;

namespace ProtonDrive.Client.Configuration;

internal sealed class ServerTimeCache : IServerTimeProvider
{
    private readonly object _serverTimeLock = new();
    private DateTimeOffset? _serverTime;

    public DateTimeOffset? ServerTime
    {
        get
        {
            lock (_serverTimeLock)
            {
                return _serverTime;
            }
        }

        set
        {
            lock (_serverTimeLock)
            {
                _serverTime = value;
            }
        }
    }

    bool IServerTimeProvider.TryGetServerTime(out DateTimeOffset serverTime)
    {
        lock (_serverTimeLock)
        {
            if (_serverTime is null)
            {
                serverTime = default;
                return false;
            }

            serverTime = _serverTime.Value;
            return true;
        }
    }

    DateTimeOffset IServerTimeProvider.GetServerTime()
    {
        return ServerTime ?? throw new InvalidOperationException("No server time available");
    }
}
