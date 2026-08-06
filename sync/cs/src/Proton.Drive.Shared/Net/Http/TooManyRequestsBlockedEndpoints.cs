using System.Collections.Concurrent;

namespace Proton.Drive.Shared.Net.Http;

public sealed class TooManyRequestsBlockedEndpoints : ConcurrentDictionary<string, DateTime>
{
}
