using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using ProtonDrive.Sync.Shared.Trees;

namespace ProtonDrive.DataAccess.Repositories;

public class AltIdentifiableTreeNodeRepository<T, TAltId> : TreeNodeRepository<T>, IAltIdentifiableTreeNodeRepository<T, long, TAltId>
    where T : class, IIdentifiableTreeNode<long>, IAltIdentifiable<long, TAltId>
    where TAltId : IEquatable<TAltId>
{
    private readonly string _getLastAltIdSql;
    private readonly string _nodeByAltIdSql;

    public AltIdentifiableTreeNodeRepository(IConnectionProvider database, string tableName, IEnumerable<string> extraColumns)
        : base(database, tableName, extraColumns.Prepend("AltId"))
    {
        // The 'COALESCE(MAX(AltId), 0)' works only for numeric TAltId types
        _getLastAltIdSql = $"SELECT COALESCE(MAX(AltId), 0) FROM {tableName}";
        _nodeByAltIdSql = $"SELECT * FROM {tableName} WHERE AltId = @AltId";
    }

    public TAltId? GetLastAltId()
    {
        return Database.Connection.QuerySingle<TAltId?>(_getLastAltIdSql) ?? default;
    }

    public virtual T? NodeByAltId(TAltId altId)
    {
        return Database.Connection.QuerySingleOrDefault<T>(_nodeByAltIdSql, new { AltId = altId });
    }
}
