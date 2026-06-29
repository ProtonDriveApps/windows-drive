namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public sealed record ProductUsedSpace(long Calendar, long Contact, long Drive, long Mail, long Pass)
{
    public static ProductUsedSpace Empty => new(0, 0, 0, 0, 0);
}
