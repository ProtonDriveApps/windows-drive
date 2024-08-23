namespace ProtonDrive.Shared;

public sealed class NullRateLimiter<TKey> : IRateLimiter<TKey>
    where TKey : notnull
{
    public bool CanExecute(TKey key)
    {
        return true;
    }

    public void DecreaseRate(TKey key)
    {
    }

    public void ResetRate(TKey key)
    {
    }

    public void Reset()
    {
    }
}
