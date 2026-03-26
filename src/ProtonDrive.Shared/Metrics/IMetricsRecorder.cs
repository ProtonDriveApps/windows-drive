namespace ProtonDrive.Shared.Metrics;

public interface IMetricsRecorder
{
    void Record(MetricEvent metricEvent);
}
