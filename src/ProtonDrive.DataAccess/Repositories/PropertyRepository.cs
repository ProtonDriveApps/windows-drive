using System.Collections.Generic;
using Dapper;
using ProtonDrive.Shared.Repository;

namespace ProtonDrive.DataAccess.Repositories;

public sealed class PropertyRepository : IPropertyRepository
{
    private readonly string _getKeysSql;
    private readonly string _getSql;
    private readonly string _addSql;
    private readonly string _updateSql;
    private readonly string _deleteSql;

    public PropertyRepository(IConnectionProvider database, string tableName)
    {
        Database = database;

        _getKeysSql = $"SELECT Key FROM {tableName}";
        _getSql = $"SELECT Value FROM {tableName} WHERE Key = @Key";
        _addSql = $"INSERT INTO {tableName}(Key, Value) VALUES (@Key, @Value)";
        _updateSql = $"UPDATE {tableName} SET Value = @Value WHERE Key = @Key";
        _deleteSql = $"DELETE FROM {tableName} WHERE Key = @Key";
    }

    private IConnectionProvider Database { get; }

    public IEnumerable<string> GetKeys()
    {
        return Database.Connection.Query<string>(_getKeysSql);
    }

    public T? Get<T>(string key)
    {
        return Database.Connection.QuerySingleOrDefault<T?>(_getSql, new { Key = key });
    }

    public void Set<T>(string key, T? value)
    {
        if (value is not null)
        {
            if (!Update(key, value))
            {
                Add(key, value);
            }
        }
        else
        {
            Delete(key);
        }
    }

    private void Add<T>(string key, T value)
    {
        Database.Connection.Execute(_addSql, new { Key = key, Value = value });
    }

    private bool Update<T>(string key, T value)
    {
        return Database.Connection.Execute(_updateSql, new { Key = key, Value = value }) != 0;
    }

    private void Delete(string key)
    {
        Database.Connection.Execute(_deleteSql, new { Key = key });
    }
}
