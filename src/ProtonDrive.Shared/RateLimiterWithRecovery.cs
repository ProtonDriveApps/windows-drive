using System;
using System.Collections.Generic;

namespace ProtonDrive.Shared;

public sealed class RateLimiterWithRecovery<TKey> : RateLimiter<TKey>
    where TKey : notnull
{
    public RateLimiterWithRecovery(IClock clock, TimeSpan minDelay, TimeSpan maxDelay)
        : base(clock, minDelay, maxDelay)
    {
    }

    protected override void HandleRecovery(TKey key, RateInfo currentRateInfo)
    {
        var elapsed = (Clock.UtcNow - currentRateInfo.LastAttemptTime).Duration();
        var n = Math.Floor(elapsed / MaxDelay);
        var delay = currentRateInfo.Delay / Math.Pow(Base, n);

        if (delay <= MinDelay / 2)
        {
            Rates.TryRemove(new KeyValuePair<TKey, RateInfo>(key, currentRateInfo));
        }
        else
        {
            delay = Max(MinDelay, delay);

            var newRateInfo = currentRateInfo with
            {
                LastAttemptTime = Clock.UtcNow,
                Delay = delay,
            };

            Rates.TryUpdate(key, newRateInfo, currentRateInfo);
        }
    }
}
