namespace Proton.Drive.Shared.Repository;

public class CollectionRepository<T> : ICollectionRepository<T>
{
    private readonly IRepository<IEnumerable<T>> _origin;

    public CollectionRepository(IRepository<IEnumerable<T>> origin)
    {
        _origin = origin;
    }

    public IReadOnlyCollection<T> GetAll()
    {
        var data = _origin.Get();

        return data as IReadOnlyCollection<T>
               ?? data?.ToList() as IReadOnlyCollection<T>
               ?? [];
    }

    public void SetAll(IEnumerable<T> value)
    {
        _origin.Set(value);
    }
}
