using System;
using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Sync.Adapter.UpdateDetection.LogBased;
using ProtonDrive.Sync.Adapter.UpdateDetection.StateBased;
using ProtonDrive.Sync.Shared.ExecutionStatistics;

namespace ProtonDrive.Sync.Adapter.UpdateDetection;

internal class UpdateDetection<TId, TAltId> : IExecutionStatisticsProvider
    where TId : IEquatable<TId>
    where TAltId : IEquatable<TAltId>
{
    private readonly StateBasedUpdateDetection<TId, TAltId> _stateBasedUpdateDetection;
    private readonly LogBasedUpdateDetection<TId, TAltId> _logBasedUpdateDetection;

    public UpdateDetection(
        StateBasedUpdateDetection<TId, TAltId> stateBasedUpdateDetection,
        LogBasedUpdateDetection<TId, TAltId> logBasedUpdateDetection)
    {
        _stateBasedUpdateDetection = stateBasedUpdateDetection;
        _logBasedUpdateDetection = logBasedUpdateDetection;
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
        await _logBasedUpdateDetection.ExecuteAsync(cancellationToken).ConfigureAwait(false);
        await _stateBasedUpdateDetection.ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }
}
