using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace Proton.Drive.Shared.Diagnostics.Metrics;

public sealed class CollectionCollector<TMeasurement>
    where TMeasurement : struct
{
    private readonly ConcurrentQueue<TMeasurement> _measurements = [];

    public ReadOnlyCollection<TMeasurement> GetMeasurementSnapshot()
    {
        var snapshot = new List<TMeasurement>(_measurements.Count);

        while (snapshot.Count < snapshot.Capacity && _measurements.TryDequeue(out var measurement))
        {
            snapshot.Add(measurement);
        }

        return snapshot.AsReadOnly();
    }

    public void RecordMeasurement(TMeasurement measurement)
    {
        _measurements.Enqueue(measurement);
    }

    public void Clear()
    {
        _measurements.Clear();
    }
}
