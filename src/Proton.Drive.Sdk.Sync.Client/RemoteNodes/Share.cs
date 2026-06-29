using Proton.Cryptography.Pgp;

namespace Proton.Drive.Sdk.Sync.Client.RemoteNodes;

internal class Share : IPrivateKeyHolder
{
    public Share(string rootLinkId, PgpPrivateKey key, string relevantMembershipAddressId)
    {
        RootLinkId = rootLinkId;
        Key = key;
        RelevantMembershipAddressId = relevantMembershipAddressId;
    }

    PgpPrivateKey IPrivateKeyHolder.PrivateKey => Key;

    public string RootLinkId { get; }
    public PgpPrivateKey Key { get; }

    /// <summary>
    /// Identifier of the address associated with the membership that the back-end deemed relevant for the current user (usually the owner membership)
    /// </summary>
    public string RelevantMembershipAddressId { get; }
}
