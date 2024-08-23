using System;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.Sync.Shared;

public sealed class IntermittentTransactedScheduler : TransactedScheduler
{
    internal static readonly TimeSpan CommitDelay = TimeSpan.FromSeconds(5);

    private readonly IClock _clock;

    private TickCount _transactionStartedAt;
    private bool _transactionCompleted = true;

    public IntermittentTransactedScheduler(
        ILogger logger,
        IScheduler originScheduler,
        ITransactionProvider transactionProvider,
        IClock clock,
        Func<Exception, bool>? isExpectedException = null)
        : base(logger, originScheduler, transactionProvider, isExpectedException)
    {
        _clock = clock;
    }

    public override bool ForceCommit { get; set; }

    protected override void BeginTransaction()
    {
        if (!_transactionCompleted)
        {
            return;
        }

        base.BeginTransaction();
        _transactionCompleted = false;
        _transactionStartedAt = _clock.TickCount;
        ForceCommit = false;
    }

    protected override void CommitTransaction()
    {
        if (!ShouldCommit())
        {
            return;
        }

        base.CommitTransaction();
        _transactionCompleted = true;
        _transactionStartedAt = default;
    }

    protected override void RollbackTransaction()
    {
        base.RollbackTransaction();
        _transactionCompleted = true;
        _transactionStartedAt = default;
    }

    protected override void EndTransaction()
    {
        if (!_transactionCompleted)
        {
            return;
        }

        base.EndTransaction();
    }

    private bool ShouldCommit() => ForceCommit || (_transactionStartedAt + CommitDelay <= _clock.TickCount);
}
