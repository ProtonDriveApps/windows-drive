namespace Proton.Drive.Shared.Telemetry;

public interface IErrorCountProvider
{
    IReadOnlyDictionary<(string ErrorKey, ErrorScope Scope), int> GetTopErrorCounts(int maximumNumberOfCounters);

    void Reset();
}
