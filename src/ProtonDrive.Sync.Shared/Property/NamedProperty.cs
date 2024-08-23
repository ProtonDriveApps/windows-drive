using ProtonDrive.Shared.Repository;

namespace ProtonDrive.Sync.Shared.Property;

public class NamedProperty<T> : IRepository<T>
{
    private readonly string _propertyName;
    private readonly IPropertyRepository _repository;

    public NamedProperty(string propertyName, IPropertyRepository repository)
    {
        _propertyName = propertyName;
        _repository = repository;
    }

    public T? Get()
    {
        return _repository.Get<T>(_propertyName);
    }

    public void Set(T? value)
    {
        _repository.Set(_propertyName, value);
    }
}
