using Proton.Drive.Sdk.Nodes;

namespace Proton.Drive.Sdk.Sync.Client;

internal static class DriveSdkUidExtensions
{
    public static string ToLinkId(this NodeUid nodeUid)
    {
        return nodeUid.ToString().Split('~')[1];
    }

    public static string ToRevisionId(this RevisionUid nodeUid)
    {
        return nodeUid.ToString().Split('~')[2];
    }
}
