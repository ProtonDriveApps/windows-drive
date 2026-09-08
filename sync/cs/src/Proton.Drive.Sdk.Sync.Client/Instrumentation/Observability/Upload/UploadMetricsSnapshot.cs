using Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Shared;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Upload;

internal sealed class UploadMetricsSnapshot
{
    public required IReadOnlyDictionary<AttemptTags, int> Attempts { get; init; }
    public required IReadOnlyDictionary<FailureTags, int> Failures { get; init; }
    public required IReadOnlyCollection<long> FailuresFileSize { get; init; }
    public required IReadOnlyCollection<long> FailuresTransferSize { get; init; }
    public required IReadOnlyCollection<TaggedUploadPerformanceMeasurement> SmallFileThroughputs { get; init; }
    public required IReadOnlyCollection<TaggedUploadPerformanceMeasurement> LargeFileThroughputs { get; init; }
    public required IReadOnlyCollection<TaggedUploadPerformanceMeasurement> LargeRouteActiveTimeShares { get; init; }
    public required IReadOnlyCollection<long> SmallRouteActiveTimeShares { get; init; }
}

internal readonly record struct TaggedUploadPerformanceMeasurement(long Value, TaggedUploadPerformanceMeasurement.MetricLabel Label)
{
    internal readonly record struct MetricLabel(string Name, string Value);
}
