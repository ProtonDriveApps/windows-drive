using Proton.Security.Cryptography.Abstractions;

namespace ProtonDrive.Client.RemoteNodes;

internal interface IPrivateKeyHolder
{
    PrivatePgpKey PrivateKey { get; }
}
