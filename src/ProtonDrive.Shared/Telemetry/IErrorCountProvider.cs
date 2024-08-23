using System.Collections.Generic;

namespace ProtonDrive.Shared.Telemetry;

public interface IErrorCountProvider
{
    IReadOnlyDictionary<(string ErrorKey, ErrorScope Scope), int> GetTopErrorCounts(int maximumNumberOfCounters);
}
