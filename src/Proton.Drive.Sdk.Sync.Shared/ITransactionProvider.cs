using System.Data;

namespace Proton.Drive.Sdk.Sync.Shared;

public interface ITransactionProvider
{
    event EventHandler TransactionStarted;
    event EventHandler TransactionEnds;
    event EventHandler TransactionCommitted;

    IDbTransaction BeginTransaction();
}
