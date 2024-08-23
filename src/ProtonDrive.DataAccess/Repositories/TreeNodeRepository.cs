using System.Collections.Generic;
using System.Linq;
using Dapper;
using Microsoft.Data.Sqlite;
using ProtonDrive.Sync.Shared.Trees;

namespace ProtonDrive.DataAccess.Repositories;

public class TreeNodeRepository<T> : ITreeNodeRepository<T, long>
    where T : class, IIdentifiableTreeNode<long>
{
    private readonly string _getLastIdSql;
    private readonly string _nodeByIdSql;
    private readonly string _childrenSql;
    private readonly string _childrenIdsSql;
    private readonly string _createSql;
    private readonly string _updateSql;
    private readonly string _deleteSql;
    private readonly string _deleteChildrenSql;
    private readonly string _clearSql;

    private long? _rootId;

    public TreeNodeRepository(IConnectionProvider database, string tableName, IEnumerable<string> extraColumns)
    {
        Database = database;

        var extraColumnList = extraColumns.ToList();
        _getLastIdSql = $"SELECT MAX(Id) FROM {tableName}";
        _nodeByIdSql = $"SELECT * FROM {tableName} WHERE Id = @Id";
        _childrenSql = $"SELECT * FROM {tableName} WHERE ParentId = @ParentId AND Id <> ParentId";
        _childrenIdsSql = $"SELECT Id FROM {tableName} WHERE ParentId = @ParentId AND Id <> ParentId";
        _createSql = $"INSERT INTO {tableName}(Id, ParentId{ExtraColumnsForInsert(extraColumnList)}) VALUES (@Id, @ParentId{ExtraValuesForInsert(extraColumnList)})";
        _updateSql = $"UPDATE {tableName} SET ParentId = @ParentId{ExtraColumnsForUpdate(extraColumnList)} WHERE Id = @Id";
        _deleteSql = $"DELETE FROM {tableName} WHERE Id = @Id";
        _deleteChildrenSql = $"DELETE FROM {tableName} WHERE ParentId = @ParentId AND Id <> ParentId";
        _clearSql = $"DELETE FROM {tableName}";
    }

    protected IConnectionProvider Database { get; init; }

    public long GetLastId()
    {
        return Database.Connection.QuerySingle<long?>(_getLastIdSql) ?? default;
    }

    public T? NodeById(long id)
    {
        var node = Database.Connection.QuerySingleOrDefault<T>(_nodeByIdSql, new { Id = id });

        if (_rootId == null && node != null && node.ParentId.Equals(node.Id))
        {
            _rootId = node.Id;
        }

        return node;
    }

    public IEnumerable<T> Children(T node)
    {
        return Database.Connection.Query<T>(_childrenSql, new { ParentId = node.Id }, buffered: false);
    }

    public IEnumerable<long> ChildrenIds(long nodeId)
    {
        return Database.Connection.Query<long>(_childrenIdsSql, new { ParentId = nodeId }, buffered: false);
    }

    public void Create(T node)
    {
        if (_rootId != null && node.ParentId.Equals(node.Id))
        {
            throw new TreeException($"Cannot create node with ParentId=Id={node.Id}");
        }

        try
        {
            var rowsAffected = Database.Connection.Execute(_createSql, node);

            if (rowsAffected != 1)
            {
                throw new TreeException($"Inserting tree Node Id={node.Id} failed");
            }

            if (node.ParentId.Equals(node.Id))
            {
                _rootId = node.Id;
            }
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19 && ex.SqliteExtendedErrorCode == 787)
        {
            // Foreign key constraint failed
            throw new TreeException($"Parent tree Node Id={node.ParentId} does not exist", ex);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19 && ex.SqliteExtendedErrorCode == 1555)
        {
            // Primary key constraint failed
            throw new TreeException($"Tree Node Id={node.Id} already exists", ex);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19 && ex.SqliteExtendedErrorCode == 2067)
        {
            // Unique constraint failed
            throw new TreeException("Tree Node with same AltId value already exists", ex);
        }
    }

    public void Update(T node)
    {
        if (_rootId != null)
        {
            if (node.Id.Equals(_rootId.Value) && !node.ParentId.Equals(node.Id))
            {
                throw new TreeException($"Cannot move the root node Id={node.Id}");
            }

            if (!node.Id.Equals(_rootId.Value) && node.ParentId.Equals(node.Id))
            {
                throw new TreeException($"Cannot update node to ParentId=Id={node.Id}");
            }
        }

        try
        {
            var rowsAffected = Database.Connection.Execute(_updateSql, node);

            if (rowsAffected == 0)
            {
                throw new TreeException($"Tree Node Id={node.Id} does not exist");
            }
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19 && ex.SqliteExtendedErrorCode == 787)
        {
            // Foreign key constraint failed
            throw new TreeException($"Parent tree Node Id={node.ParentId} does not exist", ex);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19 && ex.SqliteExtendedErrorCode == 2067)
        {
            // Unique constraint failed
            throw new TreeException("Tree Node with same AltId value already exists", ex);
        }
    }

    public void Delete(T node)
    {
        try
        {
            var rowsAffected = Database.Connection.Execute(_deleteSql, new { node.Id });

            if (rowsAffected == 0)
            {
                throw new TreeException($"Node Id={node.Id} does not exist in the tree");
            }

            if (node.Id == _rootId)
            {
                _rootId = null;
            }
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19 && ex.SqliteExtendedErrorCode == 787)
        {
            // Foreign key constraint failed
            throw new TreeException($"Cannot delete tree Node Id={node.Id} that has children", ex);
        }
    }

    public void DeleteChildren(long nodeId)
    {
        try
        {
            Database.Connection.Execute(_deleteChildrenSql, new { ParentId = nodeId });
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19 && ex.SqliteExtendedErrorCode == 787)
        {
            // Foreign key constraint failed
            throw new TreeException($"Cannot delete tree Node Id={nodeId} children", ex);
        }
    }

    public void Clear()
    {
        Database.Connection.Execute(_clearSql);
        _rootId = null;
    }

    private string ExtraColumnsForInsert(IReadOnlyCollection<string> extraColumns)
    {
        return extraColumns.Any()
            ? ", " + string.Join(", ", extraColumns)
            : string.Empty;
    }

    private string ExtraValuesForInsert(IReadOnlyCollection<string> extraColumns)
    {
        return extraColumns.Any()
            ? ", @" + string.Join(", @", extraColumns)
            : string.Empty;
    }

    private string ExtraColumnsForUpdate(IReadOnlyCollection<string> extraColumns)
    {
        return extraColumns.Any()
            ? ", " + string.Join(", ", extraColumns.Select(column => $"{column} = @{column}"))
            : string.Empty;
    }
}
