using System;

namespace ProtonDrive.Shared.Extensions;

public static class TimeSpanExtensions
{
    private static readonly Random Random = new();

    public static TimeSpan RandomizedWithDeviation(this TimeSpan value, double deviation)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Argument value must be positive");
        }

        if (deviation is < 0 or >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(deviation), "Argument value must be between zero and one");
        }

        return value + TimeSpan.FromMilliseconds(value.TotalMilliseconds * deviation * ((2.0 * Random.NextDouble()) - 1.0));
    }
}
