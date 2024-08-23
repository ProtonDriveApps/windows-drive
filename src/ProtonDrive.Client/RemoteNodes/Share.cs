using Proton.Security.Cryptography.Abstractions;

namespace ProtonDrive.Client.RemoteNodes;

internal class Share : IPrivateKeyHolder
{
    public Share(string rootLinkId, PrivatePgpKey key, string relevantMembershipAddressId)
    {
        RootLinkId = rootLinkId;
        Key = key;
        RelevantMembershipAddressId = relevantMembershipAddressId;
    }

    PrivatePgpKey IPrivateKeyHolder.PrivateKey => Key;

    public string RootLinkId { get; }
    public PrivatePgpKey Key { get; }

    /// <summary>
    /// Identifier of the address associated with the membership that the back-end deemed relevant for the current user (usually the owner membership)
    /// </summary>
    public string RelevantMembershipAddressId { get; }
}
