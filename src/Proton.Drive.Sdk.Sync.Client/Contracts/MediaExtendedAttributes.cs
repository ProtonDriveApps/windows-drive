using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public sealed class MediaExtendedAttributes
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Width { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Height { get; set; }

    /// <summary>
    /// Represents the duration of a video or audio file in seconds.
    /// Although the value is a floating-point number, it should be treated as an integer:
    /// <list type="bullet">
    /// <item><description>When reading the duration, round it down to the nearest whole number using <c>Math.Floor</c>.</description></item>
    /// <item><description>When writing the duration, store only whole seconds (discard any fractional part).</description></item>
    /// </list>
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Duration { get; set; }
}
