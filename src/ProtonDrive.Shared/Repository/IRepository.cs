namespace ProtonDrive.Shared.Repository;

public interface IRepository<T>
{
    T? Get();
    void Set(T? value);
}
