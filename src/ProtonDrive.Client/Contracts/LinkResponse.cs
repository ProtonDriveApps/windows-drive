namespace ProtonDrive.Client.Contracts;

public sealed record LinkResponse : ApiResponse
{
    public Link? Link { get; init; }
}
