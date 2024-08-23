namespace ProtonDrive.Client.Devices.Contracts;

internal sealed class DeviceLinkCreationParameters
{
    public string? NodeKey { get; set; }
    public string? NodePassphrase { get; set; }
    public string? NodePassphraseSignature { get; set; }
    public string? NodeHashKey { get; set; }
    public string? Name { get; set; }
}
