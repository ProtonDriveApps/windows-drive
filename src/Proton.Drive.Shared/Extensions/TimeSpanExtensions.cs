namespace Proton.Drive.Shared.Extensions;

public static class TimeSpanExtensions
{
    private static readonly Random Random = new();

    public static TimeSpan RandomizedWithDeviation(this TimeSpan value, double deviation, JitterDirection jitterDirection = JitterDirection.Symmetric)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Argument value must be positive");
        }

        if (deviation is < 0 or >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(deviation), "Argument value must be between zero and one");
        }

        var factor = jitterDirection switch
        {
            JitterDirection.Symmetric => (2.0 * Random.NextDouble()) - 1.0, // [-1, +1)
            JitterDirection.PositiveOnly => Random.NextDouble(), // [0, +1)
            _ => throw new ArgumentOutOfRangeException(nameof(jitterDirection), jitterDirection, null),
        };

        var jitter = TimeSpan.FromMilliseconds(value.TotalMilliseconds * deviation * factor);

        return value + jitter;
    }
}
