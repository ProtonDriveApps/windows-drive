using System;
using System.Collections.Concurrent;

namespace ProtonDrive.Shared.Net.Http;

public sealed class TooManyRequestsBlockedEndpoints : ConcurrentDictionary<string, DateTime>
{
}
