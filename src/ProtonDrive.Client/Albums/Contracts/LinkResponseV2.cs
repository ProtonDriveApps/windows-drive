namespace ProtonDrive.Client.Albums.Contracts;

public sealed record LinkResponseV2
{
    public required LinkDto Link { get; init; }
    public AlbumDto? Album { get; init; }
}
