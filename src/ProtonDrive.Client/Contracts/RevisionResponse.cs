namespace ProtonDrive.Client.Contracts;

public sealed record RevisionResponse : ApiResponse
{
    public Revision? Revision { get; init; }
}
