namespace ProtonDrive.Client.Contracts;

public interface IRevisionCreationConflictResponse
{
    public ResponseCode Code { get; }

    public RevisionCreationConflict? Conflict { get; }
}
