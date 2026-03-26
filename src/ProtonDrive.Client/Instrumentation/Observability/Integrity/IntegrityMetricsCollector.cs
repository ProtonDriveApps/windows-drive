using System.Diagnostics.Metrics;
using ProtonDrive.Client.Sdk.Metrics;
using ProtonDrive.Shared.Diagnostics.Metrics;

namespace ProtonDrive.Client.Instrumentation.Observability.Integrity;

internal sealed class IntegrityMetricsCollector
{
    private readonly AggregatingCollector<int, DecryptionFailureTags> _decryptionFailures = new();
    private readonly AggregatingCollector<int, VerificationFailureTags> _verificationFailures = new();
    private readonly AggregatingCollector<int, UploadBlockVerificationFailureTags> _uploadBlockVerificationFailures = new();
    private readonly AggregatingCollector<int, UploadChecksumVerificationAttemptTags> _uploadChecksumVerificationAttempts = new();
    private readonly AggregatingCollector<int, DownloadChecksumVerificationAttemptTags> _downloadChecksumVerificationAttempts = new();

    private MeterListener? _meterListener;
    private Instrument? _decryptionFailuresInstrument;
    private Instrument? _verificationFailuresInstrument;
    private Instrument? _uploadBlockVerificationFailuresInstrument;
    private Instrument? _uploadChecksumVerificationAttemptsInstrument;
    private Instrument? _downloadChecksumVerificationAttemptsInstrument;

    public IntegrityMetricsSnapshot GetMeasurementSnapshot()
    {
        _meterListener?.RecordObservableInstruments();

        return new IntegrityMetricsSnapshot
        {
            DecryptionFailures = _decryptionFailures.GetMeasurementSnapshot(),
            VerificationFailures = _verificationFailures.GetMeasurementSnapshot(),
            UploadBlockVerificationFailures = _uploadBlockVerificationFailures.GetMeasurementSnapshot(),
            UploadChecksumVerificationAttempts = _uploadChecksumVerificationAttempts.GetMeasurementSnapshot(),
            DownloadChecksumVerificationAttempts = _downloadChecksumVerificationAttempts.GetMeasurementSnapshot(),
        };
    }

    public void Start()
    {
        _decryptionFailures.Clear();
        _verificationFailures.Clear();
        _uploadBlockVerificationFailures.Clear();

        _meterListener = new MeterListener();
        _meterListener.SetMeasurementEventCallback<int>(OnMeasurementRecorded);

        _meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument is { Meter.Name: IntegrityMetrics.MeterName, Name: IntegrityMetrics.DecryptionErrorsMetricName })
            {
                _decryptionFailuresInstrument = instrument;
                listener.EnableMeasurementEvents(instrument);
            }

            if (instrument is { Meter.Name: IntegrityMetrics.MeterName, Name: IntegrityMetrics.VerificationErrorsMetricName })
            {
                _verificationFailuresInstrument = instrument;
                listener.EnableMeasurementEvents(instrument);
            }

            if (instrument is { Meter.Name: IntegrityMetrics.MeterName, Name: IntegrityMetrics.UploadBlockVerificationErrorsMetricName })
            {
                _uploadBlockVerificationFailuresInstrument = instrument;
                listener.EnableMeasurementEvents(instrument);
            }

            if (instrument is { Meter.Name: IntegrityMetrics.MeterName, Name: IntegrityMetrics.UploadChecksumVerificationAttemptsMetricName })
            {
                _uploadChecksumVerificationAttemptsInstrument = instrument;
                listener.EnableMeasurementEvents(instrument);
            }

            if (instrument is { Meter.Name: IntegrityMetrics.MeterName, Name: IntegrityMetrics.DownloadChecksumVerificationAttemptsMetricName })
            {
                _downloadChecksumVerificationAttemptsInstrument = instrument;
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
        if (instrument == _decryptionFailuresInstrument && DecryptionFailureTags.TryParse(tags, out var decryptionFailureKey))
        {
            _decryptionFailures.RecordMeasurement(decryptionFailureKey, measurement);
        }

        if (instrument == _verificationFailuresInstrument && VerificationFailureTags.TryParse(tags, out var verificationFailureKey))
        {
            _verificationFailures.RecordMeasurement(verificationFailureKey, measurement);
        }

        if (instrument == _uploadBlockVerificationFailuresInstrument && UploadBlockVerificationFailureTags.TryParse(tags, out var uploadBlockVerificationFailureKey))
        {
            _uploadBlockVerificationFailures.RecordMeasurement(uploadBlockVerificationFailureKey, measurement);
        }

        if (instrument == _uploadChecksumVerificationAttemptsInstrument && UploadChecksumVerificationAttemptTags.TryParse(tags, out var uploadChecksumVerificationAttemptKey))
        {
            _uploadChecksumVerificationAttempts.RecordMeasurement(uploadChecksumVerificationAttemptKey, measurement);
        }

        if (instrument == _downloadChecksumVerificationAttemptsInstrument && DownloadChecksumVerificationAttemptTags.TryParse(tags, out var downloadChecksumVerificationAttemptKey))
        {
            _downloadChecksumVerificationAttempts.RecordMeasurement(downloadChecksumVerificationAttemptKey, measurement);
        }
    }
}
