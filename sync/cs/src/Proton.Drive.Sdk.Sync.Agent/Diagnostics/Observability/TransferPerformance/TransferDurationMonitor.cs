using Proton.Drive.Shared;
using Proton.Drive.Shared.Threading;

namespace Proton.Drive.Sdk.Sync.Agent.Diagnostics.Observability.TransferPerformance;

internal sealed class TransferDurationMonitor
{
    private readonly TimeSpan _maxInactivityPeriodBetweenTransfers;
    private readonly IClock _clock;

    private TransferDurationCounter _transferDurationCounter = TransferDurationCounter.Zero;

    public TransferDurationMonitor(TimeSpan maxInactivityPeriodBetweenTransfers, IClock clock)
    {
        _maxInactivityPeriodBetweenTransfers = maxInactivityPeriodBetweenTransfers;
        _clock = clock;
    }

    public void UpdateNumberOfTransfers(int delta)
    {
        if (delta == 0)
        {
            return;
        }

        var now = _clock.TickCount;

        InterlockedExtensions.Update(ref _transferDurationCounter, UpdateValueFactory);

        return;

        TransferDurationCounter UpdateValueFactory(TransferDurationCounter previous)
        {
            var numberOfActiveTransfers = previous.NumberOfActiveTransfers + delta;
            var periodStartTime = previous.PeriodStartTime;
            var inactivityStartTime = previous.InactivityStartTime;

            if (numberOfActiveTransfers != 0 && periodStartTime == TickCount.MinValue)
            {
                periodStartTime = now;
            }

            if (numberOfActiveTransfers == 0 && inactivityStartTime == TickCount.MinValue)
            {
                inactivityStartTime = now;
            }

            if (numberOfActiveTransfers != 0 && inactivityStartTime != TickCount.MinValue)
            {
                var inactivityPeriod = now - inactivityStartTime;

                if (inactivityPeriod > _maxInactivityPeriodBetweenTransfers)
                {
                    // Exclude inactivity period by moving period start time
                    periodStartTime += inactivityPeriod;
                }

                inactivityStartTime = TickCount.MinValue;
            }

            return new TransferDurationCounter(numberOfActiveTransfers, periodStartTime, inactivityStartTime);
        }
    }

    public TimeSpan GetDurationIncrease()
    {
        var now = _clock.TickCount;
        var duration = TimeSpan.Zero;

        InterlockedExtensions.Update(ref _transferDurationCounter, UpdateValueFactory);

        return duration;

        TransferDurationCounter UpdateValueFactory(TransferDurationCounter previous)
        {
            var numberOfActiveTransfers = previous.NumberOfActiveTransfers;
            var periodStartTime = previous.PeriodStartTime;
            var inactivityStartTime = previous.InactivityStartTime;

            if (numberOfActiveTransfers != 0)
            {
                // Transfers are currently active, we return accumulated transfer duration
                duration = now - periodStartTime;
                periodStartTime = now;
                inactivityStartTime = TickCount.MinValue;
            }
            else
            {
                // Transfers are currently inactive, we return accumulated transfer duration excluding inactivity period
                duration = inactivityStartTime - periodStartTime;

                if (duration == TimeSpan.Zero)
                {
                    return previous;
                }

                periodStartTime = inactivityStartTime;
            }

            return new TransferDurationCounter(numberOfActiveTransfers, periodStartTime, inactivityStartTime);
        }
    }

    public void Clear()
    {
        Interlocked.Exchange(ref _transferDurationCounter, TransferDurationCounter.Zero);
    }

    private record TransferDurationCounter(int NumberOfActiveTransfers, TickCount PeriodStartTime, TickCount InactivityStartTime)
    {
        public static TransferDurationCounter Zero { get; } = new(0, TickCount.MinValue, TickCount.MinValue);
    }
}
