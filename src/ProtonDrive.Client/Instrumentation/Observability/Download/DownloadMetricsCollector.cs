using System.Diagnostics.Metrics;
using ProtonDrive.Client.Instrumentation.Observability.Shared;
using ProtonDrive.Client.Sdk.Metrics;
using ProtonDrive.Shared.Diagnostics.Metrics;

namespace ProtonDrive.Client.Instrumentation.Observability.Download;

internal sealed class DownloadMetricsCollector
{
    private readonly AggregatingCollector<int, AttemptTags> _attempts = new();
    private readonly AggregatingCollector<int, FailureTags> _failures = new();
    private readonly CollectionCollector<long> _failureFileSizes = new();
    private readonly CollectionCollector<long> _failureTransferSizes = new();

    private MeterListener? _meterListener;
    private Instrument? _attemptsInstrument;
    private Instrument? _failuresInstrument;
    private Instrument? _failureFileSizeInstrument;
    private Instrument? _failureTransferSizeInstrument;

    public DownloadMetricsSnapshot GetMeasurementSnapshot()
    {
        _meterListener?.RecordObservableInstruments();

        return new DownloadMetricsSnapshot
        {
            Attempts = _attempts.GetMeasurementSnapshot(),
            Failures = _failures.GetMeasurementSnapshot(),
            FailuresFileSize = _failureFileSizes.GetMeasurementSnapshot(),
            FailuresTransferSize = _failureTransferSizes.GetMeasurementSnapshot(),
        };
    }

    public void Start()
    {
        _attempts.Clear();
        _failures.Clear();
        _failureFileSizes.Clear();
        _failureTransferSizes.Clear();

        _meterListener = new MeterListener();
        _meterListener.SetMeasurementEventCallback<int>(OnMeasurementRecorded);
        _meterListener.SetMeasurementEventCallback<long>(OnMeasurementRecorded);

        _meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument is { Meter.Name: DownloadMetrics.MeterName, Name: DownloadMetrics.AttemptsMetricName })
            {
                _attemptsInstrument = instrument;
                listener.EnableMeasurementEvents(instrument);
            }

            if (instrument is { Meter.Name: DownloadMetrics.MeterName, Name: DownloadMetrics.FailuresMetricName })
            {
                _failuresInstrument = instrument;
                listener.EnableMeasurementEvents(instrument);
            }

            if (instrument is { Meter.Name: DownloadMetrics.MeterName, Name: DownloadMetrics.FailuresFileSizeMetricName })
            {
                _failureFileSizeInstrument = instrument;
                listener.EnableMeasurementEvents(instrument);
            }

            if (instrument is { Meter.Name: DownloadMetrics.MeterName, Name: DownloadMetrics.FailuresTransferSizeMetricName })
            {
                _failureTransferSizeInstrument = instrument;
                listener.EnableMeasurementEvents(instrument);
            }
        };

        _meterListener.Start();
    }

    public void Stop()
    {
        _meterListener?.Dispose();
        _meterListener = null;
    }

    private void OnMeasurementRecorded(Instrument instrument, int measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        if (instrument == _attemptsInstrument && AttemptTags.TryParse(tags, out var attemptsKey))
        {
            _attempts.RecordMeasurement(attemptsKey, measurement);
        }

        if (instrument == _failuresInstrument && FailureTags.TryParse(tags, out var failuresKey))
        {
            _failures.RecordMeasurement(failuresKey, measurement);
        }
    }

    private void OnMeasurementRecorded(Instrument instrument, long measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
    {
        if (instrument == _failureFileSizeInstrument)
        {
            _failureFileSizes.RecordMeasurement(measurement);
        }

        if (instrument == _failureTransferSizeInstrument)
        {
            _failureTransferSizes.RecordMeasurement(measurement);
        }
    }
}
