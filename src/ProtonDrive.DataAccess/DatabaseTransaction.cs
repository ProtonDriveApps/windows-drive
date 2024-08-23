using System;
using System.Data;

namespace ProtonDrive.DataAccess;

internal sealed class DatabaseTransaction : IDbTransaction
{
    private readonly Database _database;
    private readonly IDbTransaction _transaction;

    public DatabaseTransaction(Database database)
    {
        _database = database;

        try
        {
            _transaction = database.Connection.BeginTransaction();

            _database.OnTransactionStarted();
        }
        catch
        {
            _database.Fault();

            throw;
        }
    }

    public IDbConnection Connection => throw new NotSupportedException();
    public IsolationLevel IsolationLevel => throw new NotSupportedException();

    public void Commit()
    {
        try
        {
            _database.OnTransactionEnds();

            _transaction.Commit();

            _database.OnTransactionCommitted();
        }
        catch
        {
            _database.Fault();

            throw;
        }
    }

    public void Rollback()
    {
        _database.Fault();

        _transaction.Rollback();
    }

    public void Dispose()
    {
        _transaction.Dispose();
    }
}
