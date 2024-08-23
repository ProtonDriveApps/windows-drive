using System.Collections.Generic;

namespace ProtonDrive.Shared.Repository;

public interface IPropertyRepository
{
    IEnumerable<string> GetKeys();
    T? Get<T>(string key);
    void Set<T>(string key, T? value);
}
