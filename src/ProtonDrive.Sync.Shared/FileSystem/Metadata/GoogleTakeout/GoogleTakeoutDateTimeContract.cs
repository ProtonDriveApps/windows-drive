using System.Text.Json.Serialization;

namespace ProtonDrive.Sync.Shared.FileSystem.Metadata.GoogleTakeout;

internal sealed class GoogleTakeoutDateTimeContract
{
    /// <summary>
    /// Number of seconds that have elapsed since 1970-01-01T00:00:00Z
    /// </summary>
    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; init; }

    public static implicit operator DateTimeOffset?(GoogleTakeoutDateTimeContract? data)
    {
        if (string.IsNullOrEmpty(data?.Timestamp))
        {
            return null;
        }

        if (!long.TryParse(data.Timestamp, out var numberOfSeconds))
        {
            return null;
        }

        return numberOfSeconds != 0 ? DateTimeOffset.FromUnixTimeSeconds(numberOfSeconds) : null;
    }
}
