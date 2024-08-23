using Dapper;
using ProtonDrive.Sync.Adapter.Trees.Adapter.NodeLinking;

namespace ProtonDrive.DataAccess.Repositories;

public sealed class NodeLinkRepository : INodeLinkRepository<long>
{
    private readonly string _getSourceSql;
    private readonly string _getDestinationSql;
    private readonly string _addSql;
    private readonly string _deleteSql;

    public NodeLinkRepository(IConnectionProvider database, string tableName)
    {
        Database = database;

        _getSourceSql = $"SELECT SourceId FROM {tableName} WHERE Type = @Type AND DestinationId = @DestinationId";
        _getDestinationSql = $"SELECT DestinationId FROM {tableName} WHERE Type = @Type AND SourceId = @SourceId";
        _addSql = $"INSERT INTO {tableName}(Type, SourceId, DestinationId) VALUES (@Type, @SourceId, @DestinationId)";
        _deleteSql = $"DELETE FROM {tableName} WHERE Type = @Type AND SourceId = @SourceId";
    }

    private IConnectionProvider Database { get; }

    public long GetSourceNodeIdOrDefault(NodeLinkType linkType, long destinationNodeId)
    {
        return Database.Connection.QuerySingleOrDefault<long>(_getSourceSql, new { Type = linkType, DestinationId = destinationNodeId });
    }

    public long GetDestinationNodeIdOrDefault(NodeLinkType linkType, long sourceNodeId)
    {
        return Database.Connection.QuerySingleOrDefault<long>(_getDestinationSql, new { Type = linkType, SourceId = sourceNodeId });
    }

    public void Add(NodeLinkType linkType, long sourceNodeId, long destinationNodeId)
    {
        Database.Connection.Execute(_addSql, new { Type = linkType, SourceId = sourceNodeId, DestinationId = destinationNodeId });
    }

    public void Delete(NodeLinkType linkType, long sourceNodeId)
    {
        Database.Connection.Execute(_deleteSql, new { Type = linkType, SourceId = sourceNodeId });
    }
}
