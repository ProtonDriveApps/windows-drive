using Proton.Drive.Sdk.Sync.Adapter.UpdateDetection.LogBased;
using Proton.Drive.Sdk.Sync.Adapter.UpdateDetection.StateBased;
using Proton.Drive.Sdk.Sync.Shared.ExecutionStatistics;

namespace Proton.Drive.Sdk.Sync.Adapter.UpdateDetection;

internal class UpdateDetection<TId, TAltId> : IUpdateDetector
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly StateBasedUpdateDetection<TId, TAltId> _stateBasedUpdateDetection;
    private readonly LogBasedUpdateDetection<TId, TAltId> _logBasedUpdateDetection;
    private readonly TwoPassUpdateDetectionSwitch _twoPassUpdateDetectionSwitch;

    public UpdateDetection(
        StateBasedUpdateDetection<TId, TAltId> stateBasedUpdateDetection,
        LogBasedUpdateDetection<TId, TAltId> logBasedUpdateDetection,
        TwoPassUpdateDetectionSwitch twoPassUpdateDetectionSwitch)
    {
        _stateBasedUpdateDetection = stateBasedUpdateDetection;
        _logBasedUpdateDetection = logBasedUpdateDetection;
        _twoPassUpdateDetectionSwitch = twoPassUpdateDetectionSwitch;
    }

    public IExecutionStatistics ExecutionStatistics => _logBasedUpdateDetection.ExecutionStatistics +
                                                       _stateBasedUpdateDetection.ExecutionStatistics;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _stateBasedUpdateDetection.StartAsync(cancellationToken).ConfigureAwait(false);
        _logBasedUpdateDetection.Start();
    }

    public Task StopAsync()
    {
        return _logBasedUpdateDetection.StopAsync();
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await _twoPassUpdateDetectionSwitch.RefreshAsync(cancellationToken).ConfigureAwait(false);

        if (_twoPassUpdateDetectionSwitch.IsDisabled)
        {
            await _logBasedUpdateDetection.ExecuteAsync(cancellationToken).ConfigureAwait(false);
            await _stateBasedUpdateDetection.ExecuteAsync(includeDeletions: true, cancellationToken).ConfigureAwait(false);

            return;
        }

        // Update detection runs in two passes to avoid wrongly deleting nodes that are moved while the
        // state-based enumeration is in progress. State-based deletion detection infers a deletion from a
        // node being absent from its parent folder. If a node is moved into a folder that has already been
        // enumerated, the enumeration never observes it at its new location.
        //
        // First pass: enumerate without deletion detection, so a concurrent move operation is left as a
        // dirty node instead of being treated as a deletion. We then run a second pass,
        // where log-based detection resolves the move to its new location.
        await _logBasedUpdateDetection.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        await _stateBasedUpdateDetection.ExecuteAsync(includeDeletions: false, cancellationToken).ConfigureAwait(false);

        // FIXME: remote event throttling prevents the reception of potential move and rename events that are required to avoid erroneous deletions
        // TODO: skip the second pass when the first pass detects no pending deletions
        await _logBasedUpdateDetection.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        await _stateBasedUpdateDetection.ExecuteAsync(includeDeletions: true, cancellationToken).ConfigureAwait(false);
    }
}
