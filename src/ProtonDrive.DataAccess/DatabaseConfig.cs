using Microsoft.Data.Sqlite;

namespace ProtonDrive.DataAccess;

public sealed class DatabaseConfig
{
    public DatabaseConfig(string databaseFileName)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databaseFileName,
            Cache = SqliteCacheMode.Private,
            ForeignKeys = true,
            RecursiveTriggers = false,
            Pooling = false,
        };

        ConnectionString = builder.ConnectionString;
    }

    public string ConnectionString { get; }
}
