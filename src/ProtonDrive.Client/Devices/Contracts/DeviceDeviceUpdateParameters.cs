using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Devices.Contracts;

internal sealed class DeviceDeviceUpdateParameters
{
    [JsonPropertyName("SyncState")]
    [JsonConverter(typeof(BooleanToIntegerJsonConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsSynchronizationEnabled { get; set; }
}
