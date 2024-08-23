using System;
using System.Data;

namespace ProtonDrive.Sync.Shared;

public interface ITransactionProvider
{
    event EventHandler TransactionStarted;
    event EventHandler TransactionEnds;
    event EventHandler TransactionCommitted;

    IDbTransaction BeginTransaction();
}
