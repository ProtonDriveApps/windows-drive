namespace Proton.Drive.Shared.Repository;

public interface ICollectionRepository<T>
{
    IReadOnlyCollection<T> GetAll();

    void SetAll(IEnumerable<T> value);
}
