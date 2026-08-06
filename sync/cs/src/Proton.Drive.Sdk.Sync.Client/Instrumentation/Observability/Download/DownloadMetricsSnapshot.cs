using Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Shared;

namespace Proton.Drive.Sdk.Sync.Client.Instrumentation.Observability.Download;

internal sealed class DownloadMetricsSnapshot
{
    public required IReadOnlyDictionary<AttemptTags, int> Attempts { get; init; }
    public required IReadOnlyDictionary<FailureTags, int> Failures { get; init; }
    public required IReadOnlyCollection<long> FailuresFileSize { get; init; }
    public required IReadOnlyCollection<long> FailuresTransferSize { get; init; }
}
