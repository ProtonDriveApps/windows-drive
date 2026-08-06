namespace Proton.Drive.Shared;

public interface IRateLimiter<in TKey>
    where TKey : notnull
{
    bool CanExecute(TKey key);
    void DecreaseRate(TKey key);
    void ResetRate(TKey key);
    void Reset();
}
