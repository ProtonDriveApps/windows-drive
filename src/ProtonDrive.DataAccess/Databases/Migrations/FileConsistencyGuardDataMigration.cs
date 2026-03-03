using System.Data;
using Dapper;

namespace ProtonDrive.DataAccess.Databases.Migrations;

internal class FileConsistencyGuardDataMigration
{
    private const string GetPropertySql = "SELECT Value FROM Properties WHERE Key = @Key";
    private const string AddPropertySql = "INSERT INTO Properties(Key, Value) VALUES (@Key, @Value)";
    private const string UpdatePropertySql = "UPDATE Properties SET Value = @Value WHERE Key = @Key";
    private const string DeletePropertySql = "DELETE FROM Properties WHERE Key = @Key";

    private const string DataVersionPropertyKey = "DataVersion";

    private readonly IDbConnection _connection;

    public FileConsistencyGuardDataMigration(IDbConnection connection)
    {
        _connection = connection;
    }

    public void Execute()
    {
        MigrateToVersion1();
        MigrateToVersion2();
    }

    private void MigrateToVersion1()
    {
        var dataVersion = GetProperty<int?>(DataVersionPropertyKey) ?? 0;

        if (dataVersion != 0)
        {
            return;
        }

        using var transaction = _connection.BeginTransaction();

        const string resetStatusToNoneSql =
            """
            UPDATE Files SET
                Status = 0,     -- None
                Reason = 0,     -- None,
                Error = 0       -- None
            WHERE
                Reason = 6      -- SizeMismatch
             OR Reason = 7      -- LastBytes
             OR Reason = 10     -- SizeRule
            """;

        _connection.Execute(resetStatusToNoneSql);

        const string updateStatusFromSkippedToConsistentSql =
            """
            UPDATE Files SET
                Status = 2      -- Consistent
            WHERE
                Status = 1      -- Skipped
            AND Reason = 5      -- SizeZero
            """;

        _connection.Execute(updateStatusFromSkippedToConsistentSql);

        SetProperty(DataVersionPropertyKey, 1);

        transaction.Commit();
    }

    private void MigrateToVersion2()
    {
        var dataVersion = GetProperty<int?>(DataVersionPropertyKey) ?? 0;

        if (dataVersion != 1)
        {
            return;
        }

        using var transaction = _connection.BeginTransaction();

        const string updateStatusFromSkippedToConsistentSql =
            """
            UPDATE Files SET
                Status = 2      -- Consistent
            WHERE
                Status = 1      -- Skipped
            AND Reason = 1      -- Partial
            """;

        _connection.Execute(updateStatusFromSkippedToConsistentSql);

        const string updateStatusFromInconsistentToSkippedSql =
            """
            UPDATE Files SET
                Status = 1,     -- Skipped
                Reason = 12     -- HashUnknown
            WHERE
                Status = 3      -- Inconsistent
            AND Reason = 7      -- LastBytes
            """;

        _connection.Execute(updateStatusFromInconsistentToSkippedSql);

        SetProperty(DataVersionPropertyKey, 2);

        transaction.Commit();
    }

    private T? GetProperty<T>(string key)
    {
        return _connection.QuerySingleOrDefault<T?>(GetPropertySql, new { Key = key });
    }

    private void SetProperty<T>(string key, T? value)
    {
        if (value is not null)
        {
            if (!UpdateProperty(key, value))
            {
                AddProperty(key, value);
            }
        }
        else
        {
            DeleteProperty(key);
        }
    }

    private void AddProperty<T>(string key, T value)
    {
       _connection.Execute(AddPropertySql, new { Key = key, Value = value });
    }

    private bool UpdateProperty<T>(string key, T value)
    {
        return _connection.Execute(UpdatePropertySql, new { Key = key, Value = value }) != 0;
    }

    private void DeleteProperty(string key)
    {
        _connection.Execute(DeletePropertySql, new { Key = key });
    }
}
