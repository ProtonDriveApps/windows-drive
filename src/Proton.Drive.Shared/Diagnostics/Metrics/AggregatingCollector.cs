using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Numerics;

namespace Proton.Drive.Shared.Diagnostics.Metrics;

public sealed class AggregatingCollector<TMeasurement, TKey>
    where TMeasurement : struct, INumber<TMeasurement>
    where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, TMeasurement> _measurements = [];

    public ReadOnlyDictionary<TKey, TMeasurement> GetMeasurementSnapshot()
    {
        var snapshot = new Dictionary<TKey, TMeasurement>();

        foreach (var (key, value) in _measurements)
        {
            if (value == default)
            {
                continue;
            }

            _measurements.AddOrUpdate(key, _ => default, (_, measurement) => measurement - value);
            snapshot.Add(key, value);
        }

        return snapshot.AsReadOnly();
    }

    public void RecordMeasurement(TKey key, TMeasurement measurement)
    {
        _measurements.AddOrUpdate(key, _ => measurement, (_, value) => value + measurement);
    }

    public void Clear()
    {
        _measurements.Clear();
    }
}
