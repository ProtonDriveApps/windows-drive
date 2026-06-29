namespace Proton.Drive.Sdk.Sync.Shared.FileSystem;

public sealed record FileContentChecksum
{
    public ReadOnlyMemory<byte>? Sha1 { get; init; }
    public bool Sha1Verified { get; init; }

    public static FileContentChecksum Empty { get; } = new();
}
