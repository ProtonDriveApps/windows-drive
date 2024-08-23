namespace ProtonDrive.Shared;

public interface IRateLimiter<TKey>
    where TKey : notnull
{
    bool CanExecute(TKey key);
    void DecreaseRate(TKey key);
    void ResetRate(TKey key);
    void Reset();
}
