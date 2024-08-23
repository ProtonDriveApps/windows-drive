using System;
using System.Collections.Generic;
using System.Linq;

namespace ProtonDrive.Shared.Repository;

public class CollectionRepository<T> : ICollectionRepository<T>
{
    private readonly IRepository<IEnumerable<T>> _origin;

    public CollectionRepository(IRepository<IEnumerable<T>> origin)
    {
        _origin = origin;
    }

    public ICollection<T> GetAll()
    {
        var data = _origin.Get();

        return data as ICollection<T>
               ?? data?.ToList() as ICollection<T>
               ?? Array.Empty<T>();
    }

    public void SetAll(IEnumerable<T> value)
    {
        _origin.Set(value);
    }
}
