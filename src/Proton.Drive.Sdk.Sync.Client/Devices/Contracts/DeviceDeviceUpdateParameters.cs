using System.Text.Json.Serialization;

namespace Proton.Drive.Sdk.Sync.Client.Devices.Contracts;

internal sealed class DeviceDeviceUpdateParameters
{
    [JsonPropertyName("SyncState")]
    [JsonConverter(typeof(BooleanToIntegerJsonConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsSynchronizationEnabled { get; set; }
}
