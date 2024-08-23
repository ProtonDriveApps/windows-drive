using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dapper;
using ProtonDrive.Sync.Shared.Trees;

namespace ProtonDrive.DataAccess.Repositories;

public sealed class LooseCompoundAltIdentifiableTreeNodeRepository<T, TFlattened, TAltId> :
    ILooseCompoundAltIdentifiableTreeNodeRepository<T, long, TAltId>
    where T : class, IIdentifiableTreeNode<long>, ILooseCompoundAltIdentifiable<long, TAltId>
    where TFlattened : class, IIdentifiableTreeNode<long>
    where TAltId : IEquatable<TAltId>
{
    private readonly IConnectionProvider _database;
    private readonly IFlatteningConverter<T, TFlattened> _converter;

    private readonly string _nodeByAltIdSql;
    private readonly TreeNodeRepository<TFlattened> _repository;

    public LooseCompoundAltIdentifiableTreeNodeRepository(
        IConnectionProvider database,
        string tableName,
        IEnumerable<string> extraColumns,
        IFlatteningConverter<T, TFlattened> converter)
    {
        _database = database;
        _converter = converter;

        _nodeByAltIdSql = $"SELECT * FROM {tableName} WHERE VolumeId = @VolumeId AND AltId = @AltId";

        _repository = new TreeNodeRepository<TFlattened>(database, tableName, extraColumns.Prepend("VolumeId").Prepend("AltId"));
    }

    public long GetLastId()
    {
        return _repository.GetLastId();
    }

    public T? NodeById(long id)
    {
        return FromFlattened(_repository.NodeById(id));
    }

    public void Create(T node)
    {
        _repository.Create(ToFlattened(node));
    }

    public void Update(T node)
    {
        _repository.Update(ToFlattened(node));
    }

    public void Delete(T node)
    {
        _repository.Delete(ToFlattened(node));
    }

    public void DeleteChildren(long nodeId)
    {
        _repository.DeleteChildren(nodeId);
    }

    public void Clear()
    {
        _repository.Clear();
    }

    public IEnumerable<T> Children(T node)
    {
        return _repository.Children(ToFlattened(node)).Select(_converter.FromFlattened);
    }

    public IEnumerable<long> ChildrenIds(long nodeId)
    {
        return _repository.ChildrenIds(nodeId);
    }

    public LooseCompoundAltIdentity<TAltId> GetLastAltId()
    {
        throw new NotSupportedException();
    }

    public T? NodeByAltId(LooseCompoundAltIdentity<TAltId> altId)
    {
        if (altId.Equals(default))
        {
            throw new ArgumentException("Argument value must be not default", nameof(altId));
        }

        return FromFlattened(_database.Connection.QuerySingleOrDefault<TFlattened?>(_nodeByAltIdSql, new { altId.VolumeId, AltId = altId.ItemId }));
    }

    [return: NotNullIfNotNull("node")]
    private TFlattened? ToFlattened(T? node)
    {
        return node == null ? null : _converter.ToFlattened(node);
    }

    [return: NotNullIfNotNull("flattened")]
    private T? FromFlattened(TFlattened? flattened)
    {
        return flattened == null ? null : _converter.FromFlattened(flattened);
    }
}
