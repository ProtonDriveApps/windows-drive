using System;
using System.Collections.Concurrent;

namespace ProtonDrive.Shared;

public class RateLimiter<TKey> : IRateLimiter<TKey>
    where TKey : notnull
{
    private const int MinNumberOfStepsBeforeReachingMaxDelay = 2;
    private const int MaxNumberOfStepsBeforeReachingMaxDelay = 6;
    private const double PreferredBase = 2.0;

    public RateLimiter(IClock clock, TimeSpan minDelay, TimeSpan maxDelay)
    {
        if (minDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(minDelay));
        }

        if (maxDelay < minDelay)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDelay));
        }

        Clock = clock;
        MinDelay = minDelay;
        MaxDelay = maxDelay;

        // The delay is calculated as (minDelay * base ^ (n - 1)) where n is number of times rate was decreased
        var n = Convert.ToInt32(Math.Round(Math.Log(maxDelay / minDelay, PreferredBase)));
        n = Math.Max(n, MinNumberOfStepsBeforeReachingMaxDelay);
        n = Math.Min(n, MaxNumberOfStepsBeforeReachingMaxDelay);
        Base = Math.Pow(maxDelay / minDelay, 1.0 / (n - 1));
    }

    protected ConcurrentDictionary<TKey, RateInfo> Rates { get; } = new();
    protected IClock Clock { get; }
    protected TimeSpan MinDelay { get; }
    protected TimeSpan MaxDelay { get; }
    protected double Base { get; }

    public bool CanExecute(TKey key)
    {
        if (!Rates.TryGetValue(key, out var currentRateInfo))
        {
            return true;
        }

        var elapsed = (Clock.UtcNow - currentRateInfo.LastRateDecreaseTime).Duration();

        HandleRecovery(key, currentRateInfo);

        return elapsed >= currentRateInfo.Delay;
    }

    public void DecreaseRate(TKey key)
    {
        var now = Clock.UtcNow;

        if (Rates.TryGetValue(key, out var currentRateInfo))
        {
            var newDelay = Min(MaxDelay, currentRateInfo.Delay.Multiply(Base));
            var newRateInfo = new RateInfo(now, now, newDelay);

            Rates.TryUpdate(key, newRateInfo, currentRateInfo);
        }
        else
        {
            Rates.TryAdd(key, new RateInfo(now, now, MinDelay));
        }
    }

    public void ResetRate(TKey key)
    {
        Rates.TryRemove(key, out _);
    }

    public void Reset()
    {
        Rates.Clear();
    }

    protected static TimeSpan Min(TimeSpan x, TimeSpan y)
    {
        return x.CompareTo(y) <= 0 ? x : y;
    }

    protected static TimeSpan Max(TimeSpan x, TimeSpan y)
    {
        return x.CompareTo(y) >= 0 ? x : y;
    }

    protected virtual void HandleRecovery(TKey key, RateInfo currentRateInfo)
    {
    }

    protected record RateInfo(DateTime LastRateDecreaseTime, DateTime LastAttemptTime, TimeSpan Delay);
}
