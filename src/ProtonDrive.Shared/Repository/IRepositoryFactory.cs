namespace ProtonDrive.Shared.Repository;

public interface IRepositoryFactory
{
    ICollectionRepository<T> GetCachingCollectionRepository<T>(string fileName);
    ICollectionRepository<T> GetCollectionRepository<T>(string fileName);
    IRepository<T> GetCachingRepository<T>(string fileName);
    IRepository<T> GetRepository<T>(string fileName);
}
