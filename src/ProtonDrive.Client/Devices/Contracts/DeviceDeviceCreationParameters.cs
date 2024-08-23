using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Devices.Contracts;

internal sealed class DeviceDeviceCreationParameters
{
    [JsonPropertyName("VolumeID")]
    public string? VolumeId { get; set; }

    public DevicePlatform Type { get; set; }

    [JsonPropertyName("SyncState")]
    [JsonConverter(typeof(BooleanToIntegerJsonConverter))]
    public bool IsSynchronizationEnabled { get; set; } = true;
}
