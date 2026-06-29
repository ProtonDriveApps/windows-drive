namespace Proton.Drive.Sdk.Sync.Client.Shares.Contracts;

internal sealed class LinkCreationParameters
{
    public required string NodeKey { get; init; }
    public required string NodePassphrase { get; init; }
    public required string NodePassphraseSignature { get; init; }
    public required string NodeHashKey { get; init; }
    public required string Name { get; init; }
}
