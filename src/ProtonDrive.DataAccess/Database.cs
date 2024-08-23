using System;
using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using ProtonDrive.DataAccess.Repositories;
using ProtonDrive.Shared;
using ProtonDrive.Shared.Repository;
using ProtonDrive.Sync.Shared;

namespace ProtonDrive.DataAccess;

public abstract class Database : IConnectionProvider, ITransactionProvider
{
    private readonly DatabaseConfig _config;

    private IDbConnection? _connection;

    protected Database(DatabaseConfig config)
    {
        _config = config;

        PropertyRepository = new PropertyRepository(this, "Properties");
    }

    public event EventHandler? TransactionStarted;
    public event EventHandler? TransactionEnds;
    public event EventHandler? TransactionCommitted;
    public event EventHandler? Faulted;

    public bool IsFaulty { get; private set; }

    public IPropertyRepository PropertyRepository { get; }

    public IDbConnection Connection
    {
        get => _connection ?? throw new InvalidOperationException("Database not opened");
    }

    public IDbTransaction BeginTransaction()
    {
        if (IsFaulty)
        {
            throw new FaultyStateException("Database is faulty");
        }

        return new DatabaseTransaction(this);
    }

    public void Open()
    {
        if (_connection != null)
        {
            return;
        }

        var connection = new SqliteConnection(_config.ConnectionString);
        connection.Open();

        SetupDatabase(connection);

        _connection = connection;
    }

    public void Close()
    {
        if (_connection == null)
        {
            return;
        }

        _connection.Close();
        _connection.Dispose();
        _connection = null;
    }

    internal void OnTransactionStarted()
    {
        TransactionStarted?.Invoke(this, EventArgs.Empty);
    }

    internal void OnTransactionEnds()
    {
        TransactionEnds?.Invoke(this, EventArgs.Empty);
    }

    internal void OnTransactionCommitted()
    {
        TransactionCommitted?.Invoke(this, EventArgs.Empty);
    }

    internal void Fault()
    {
        var faulted = !IsFaulty;
        IsFaulty = true;

        if (faulted)
        {
            Faulted?.Invoke(this, EventArgs.Empty);
        }
    }

    protected virtual void SetupDatabase(IDbConnection connection)
    {
        connection.Execute("CREATE TABLE IF NOT EXISTS Properties(" +
                           "Key TEXT NOT NULL PRIMARY KEY ASC, " +
                           "Value TEXT)");
    }
}
