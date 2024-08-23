using System.Text.Json.Serialization;

namespace ProtonDrive.Client.Settings.Contracts;

public sealed record SettingsResponse : ApiResponse
{
    private GeneralSettings? _settings;

    [JsonPropertyName("UserSettings")]
    public GeneralSettings Settings
    {
        get => _settings ??= new();
        init => _settings = value;
    }
}
