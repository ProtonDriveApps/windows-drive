using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Microsoft.Data.Sqlite;
using ProtonDrive.Sync.Shared.Trees;
using ProtonDrive.Sync.Shared.Trees.Changes;
using ProtonDrive.Sync.Shared.Trees.FileSystem;
using ProtonDrive.Sync.Shared.Trees.Operations;

namespace ProtonDrive.DataAccess.Repositories;

public sealed class TreeChangeRepository : ITreeChangeRepository<long>
{
    private readonly string _getLastNodeIdSql;
    private readonly string _getAllSql;
    private readonly string _containsNodeSql;
    private readonly string _addSql;
    private readonly string _deleteUpToSql;

    public TreeChangeRepository(IConnectionProvider database, string tableName)
    {
        Database = database;

        _getLastNodeIdSql = $"SELECT MAX(NodeId) FROM {tableName}";
        _getAllSql = $"SELECT * FROM {tableName} ORDER BY Id ASC";
        _containsNodeSql = $"SELECT EXISTS(SELECT 1 FROM {tableName} WHERE NodeId = @NodeId LIMIT 1)";
        _addSql = $"INSERT INTO {tableName}" +
                  "(Id, OperationType, NodeId, NodeParentId, NodeType, NodeName, NodeContentVersion) VALUES " +
                  "(@Id, @OperationType, @NodeId, @NodeParentId, @NodeType, @NodeName, @NodeContentVersion)";
        _deleteUpToSql = $"DELETE FROM {tableName} WHERE Id <= @Id";
    }

    private IConnectionProvider Database { get; }

    public long GetLastNodeId()
    {
        return Database.Connection.QuerySingle<long?>(_getLastNodeIdSql) ?? default;
    }

    public IEnumerable<TreeChange<long>> GetAll()
    {
        return Database.Connection
            .Query<Entry>(_getAllSql, buffered: false)
            .Select(row => new TreeChange<long>(
                row.Id,
                new Operation<FileSystemNodeModel<long>>(
                    row.OperationType,
                    new FileSystemNodeModel<long>
                    {
                        Id = row.NodeId,
                        ParentId = row.NodeParentId,
                        Type = row.NodeType,
                        Name = row.NodeName,
                        ContentVersion = row.NodeContentVersion,
                    })));
    }

    public bool ContainsNode(long id)
    {
        return Database.Connection.QuerySingle<bool>(_containsNodeSql, new { NodeId = id });
    }

    public void Add(TreeChange<long> item)
    {
        try
        {
            var model = item.Operation.Model;
            var entry = new Entry
            {
                Id = item.Id,
                OperationType = item.Operation.Type,
                NodeId = model.Id,
                NodeParentId = model.ParentId,
                NodeType = model.Type,
                NodeName = model.Name,
                NodeContentVersion = model.ContentVersion,
            };

            var rowsAffected = Database.Connection.Execute(_addSql, entry);

            if (rowsAffected != 1)
            {
                throw new TreeException($"Inserting row with Id={item.Id} failed");
            }
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19 && ex.SqliteExtendedErrorCode == 1555)
        {
            // Primary key constraint failed
            throw new Exception($"Row with Id={item.Id} already exists", ex);
        }
    }

    public void DeleteUpTo(long id)
    {
        Database.Connection.Execute(_deleteUpToSql, new { Id = id });
    }

    private record Entry
    {
        public long Id { get; init; }
        public OperationType OperationType { get; init; }
        public long NodeId { get; init; }
        public long NodeParentId { get; init; }
        public NodeType NodeType { get; init; }
        public string NodeName { get; init; } = string.Empty;
        public long NodeContentVersion { get; init; }
    }
}
