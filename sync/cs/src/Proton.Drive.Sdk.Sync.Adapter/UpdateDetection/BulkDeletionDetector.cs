using Proton.Drive.Sdk.Sync.Shared.ExecutionStatistics;

namespace Proton.Drive.Sdk.Sync.Adapter.UpdateDetection;

internal sealed class BulkDeletionDetector : IBulkDeletionDetector
{
    internal const int MinimumDeletionsToStartDetection = 20;
    internal const int MinimumDeletionsToContinueDetection = 1000;

    private bool _deletionInProgress;
    private int _previousNumberOfDeletedNodes;

    public void BeginDetection(IExecutionStatistics executionStatistics)
    {
        _deletionInProgress = false;
        _previousNumberOfDeletedNodes = executionStatistics.Deleted;
    }

    public bool ContinueDetection(IExecutionStatistics executionStatistics)
    {
        var latestNumberOfDeletedNodes = executionStatistics.Deleted;
        var numberOfDeletedNodes = latestNumberOfDeletedNodes - _previousNumberOfDeletedNodes;
        _previousNumberOfDeletedNodes = latestNumberOfDeletedNodes;

        _deletionInProgress =
            (!_deletionInProgress && numberOfDeletedNodes >= MinimumDeletionsToStartDetection) ||
            (_deletionInProgress && numberOfDeletedNodes >= MinimumDeletionsToContinueDetection);

        return _deletionInProgress;
    }
}
