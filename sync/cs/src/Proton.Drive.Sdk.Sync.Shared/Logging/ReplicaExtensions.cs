namespace Proton.Drive.Sdk.Sync.Shared.Logging;

public static class ReplicaExtensions
{
    public static string ToLogScope(this Replica replica) => replica switch
    {
        Replica.Local => "L",
        Replica.Remote => "R",
        _ => throw new ArgumentOutOfRangeException(nameof(replica), replica, null),
    };
}
