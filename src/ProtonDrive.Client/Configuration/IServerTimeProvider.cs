using System;

namespace ProtonDrive.Client.Configuration;

public interface IServerTimeProvider
{
    bool TryGetServerTime(out DateTimeOffset serverTime);
    DateTimeOffset GetServerTime();
}
