namespace ProtonDrive.Client.Contracts;

public sealed class PublicKeyEntry
{
    public PublicKeyFlags Flags { get; set; }

    public string PublicKey { get; set; } = string.Empty;
}
