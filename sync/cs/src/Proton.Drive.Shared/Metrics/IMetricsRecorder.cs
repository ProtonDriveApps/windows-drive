namespace Proton.Drive.Shared.Metrics;

public interface IMetricsRecorder
{
    void Record(MetricEvent metricEvent);
}
