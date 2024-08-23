using ProtonDrive.Shared.Repository;

namespace ProtonDrive.Sync.Shared.Property;

public class PersistentIdentitySource<TId> : IIdentitySource<TId>
{
    private readonly IIdentitySource<TId> _origin;
    private readonly IRepository<TId> _repository;

    private bool _initialized;

    public PersistentIdentitySource(
        IIdentitySource<TId> origin,
        IRepository<TId> repository)
    {
        _origin = origin;
        _repository = repository;
    }

    public void InitializeFrom(TId? value)
    {
        if (value is not null)
        {
            _origin.InitializeFrom(value);
        }

        var lastValue = _repository.Get();
        if (lastValue is not null)
        {
            _origin.InitializeFrom(lastValue);
        }

        _initialized = true;
    }

    public TId NextValue()
    {
        if (!_initialized)
        {
            InitializeFrom(default);
        }

        var value = _origin.NextValue();
        _repository.Set(value);

        return value;
    }
}
