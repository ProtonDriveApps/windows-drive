using System.Collections.Generic;

namespace ProtonDrive.Shared.Repository;

public interface ICollectionRepository<T>
{
    ICollection<T> GetAll();

    void SetAll(IEnumerable<T> value);
}
