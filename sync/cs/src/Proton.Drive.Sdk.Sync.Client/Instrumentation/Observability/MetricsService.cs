using System.Collections.Immutable;
using Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Download;
using Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Integrity;
using Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Upload;
using Proton.Drive.Shared;
using Proton.Drive.Shared.Configuration;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability;

internal sealed class MetricsService : IMetricsService
{
    private readonly IClock _clock;
    private readonly UploadMetricsMapper _uploadMetricsMapper;
    private readonly DownloadMetricsMapper _downloadMetricsMapper;
    private readonly IntegrityMetricsMapper _integrityMetricsMapper;

    private readonly TimeSpan _failuresImpactedUsersReportInterval;
    private TickCount _nextFailuresImpactedUsersReportTime;

    public MetricsService(
        AppConfig appConfig,
        IClock clock,
        UploadMetricsMapper uploadMetricsMapper,
        DownloadMetricsMapper downloadMetricsMapper,
        IntegrityMetricsMapper integrityMetricsMapper)
    {
        _clock = clock;
        _uploadMetricsMapper = uploadMetricsMapper;
        _downloadMetricsMapper = downloadMetricsMapper;
        _integrityMetricsMapper = integrityMetricsMapper;

        _failuresImpactedUsersReportInterval = appConfig.PeriodicFailuresImpactedUsersReportInterval;
        _nextFailuresImpactedUsersReportTime = clock.TickCount + _failuresImpactedUsersReportInterval;
    }

    public void Start()
    {
        _uploadMetricsMapper.Start();
        _downloadMetricsMapper.Start();
        _integrityMetricsMapper.Start();
    }

    public void Stop()
    {
        _uploadMetricsMapper.Stop();
        _downloadMetricsMapper.Stop();
        _integrityMetricsMapper.Stop();
    }

    public ImmutableList<ObservabilityMetric> GetMetrics(bool userHasAPaidPlan)
    {
        return _uploadMetricsMapper.GetMetrics()
            .AddRange(_downloadMetricsMapper.GetMetrics())
            .AddRange(_integrityMetricsMapper.GetMetrics())
            .AddRange(GetFailuresImpactedUserMetrics(userHasAPaidPlan));
    }

    private ImmutableList<ObservabilityMetric> GetFailuresImpactedUserMetrics(bool userHasAPaidPlan)
    {
        var now = _clock.TickCount;

        // Failures impacted users are reported less often than other metrics
        if (now < _nextFailuresImpactedUsersReportTime)
        {
            return [];
        }

        var userPlan = userHasAPaidPlan ? "paid" : "free";
        _nextFailuresImpactedUsersReportTime = now + _failuresImpactedUsersReportInterval;

        return _uploadMetricsMapper.GetFailuresImpactedUserMetrics(userPlan)
            .AddRange(_downloadMetricsMapper.GetFailuresImpactedUserMetrics(userPlan))
            .AddRange(_integrityMetricsMapper.GetFailuresImpactedUserMetrics(userPlan));
    }
}
