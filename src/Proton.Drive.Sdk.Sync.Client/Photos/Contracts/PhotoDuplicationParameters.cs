namespace Proton.Drive.Sdk.Sync.Client.Photos.Contracts;

public sealed record PhotoDuplicationParameters
{
    public IReadOnlyCollection<string> NameHashes { get; init; } = [];
}
