using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProtonDrive.Shared.Threading;

namespace ProtonDrive.Sync.Shared;

public class TransactedScheduler : ITransactedScheduler
{
    private readonly ILogger _logger;
    private readonly IScheduler _originScheduler;
    private readonly ITransactionProvider _transactionProvider;
    private readonly Func<Exception, bool>? _isExpectedException;

    private IDbTransaction? _transaction;

    public TransactedScheduler(ILogger logger, IScheduler originScheduler, ITransactionProvider transactionProvider, Func<Exception, bool>? isExpectedException = null)
    {
        _logger = logger;
        _originScheduler = originScheduler;
        _transactionProvider = transactionProvider;
        _isExpectedException = isExpectedException;
    }

    public virtual bool ForceCommit
    {
        get => true;
        set
        {
            if (!value)
            {
                throw new InvalidOperationException();
            }
        }
    }

    public Task<T> Schedule<T>(Func<Task<T>> function)
    {
        return _originScheduler.Schedule(() => Transacted(function));
    }

    public ISchedulerTimer CreateTimer()
    {
        throw new NotSupportedException();
    }

    protected virtual void BeginTransaction()
    {
        _transaction = _transactionProvider.BeginTransaction();
        _logger.LogTrace("Transaction started");
    }

    protected virtual void CommitTransaction()
    {
        _transaction!.Commit();
        _logger.LogTrace("Transaction committed");
    }

    protected virtual void EndTransaction()
    {
        _transaction!.Dispose();
        _transaction = null;
    }

    protected virtual void RollbackTransaction()
    {
        _transaction!.Rollback();
        _logger.LogTrace("Transaction rolled back");
    }

    private async Task<T> Transacted<T>(Func<Task<T>> function)
    {
        BeginTransaction();

        try
        {
            var result = await function().ConfigureAwait(false);

            CommitTransaction();

            return result;
        }
        catch (OperationCanceledException)
        {
            // Cancellation is always expected
            CommitTransaction();

            throw;
        }
        catch (Exception ex) when (IsExpectedException(ex))
        {
            CommitTransaction();

            throw;
        }
        catch
        {
            RollbackTransaction();

            throw;
        }
        finally
        {
            EndTransaction();
        }
    }

    private bool IsExpectedException(Exception ex)
    {
        return _isExpectedException?.Invoke(ex) ?? false;
    }
}
