namespace ProtonDrive.Client.Albums.Contracts;

public sealed record LinkResponseListV2 : ApiResponse
{
    public required IReadOnlyList<LinkResponseV2> Links { get; init; }
}
