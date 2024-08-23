using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Devices.Contracts;

internal sealed class DeviceShareUpdateParameters
{
    /// <summary>
    /// Device name
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }
}
