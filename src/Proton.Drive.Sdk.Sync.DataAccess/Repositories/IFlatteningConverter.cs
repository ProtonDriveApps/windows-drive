namespace Proton.Drive.Sdk.Sync.DataAccess.Repositories;

public interface IFlatteningConverter<T, TFlattened>
{
    TFlattened ToFlattened(T item);
    T FromFlattened(TFlattened flattened);
}
