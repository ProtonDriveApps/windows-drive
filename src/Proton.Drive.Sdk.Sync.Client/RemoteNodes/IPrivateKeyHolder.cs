using Proton.Cryptography.Pgp;

namespace Proton.Drive.Sdk.Sync.Client.RemoteNodes;

internal interface IPrivateKeyHolder
{
    PgpPrivateKey PrivateKey { get; }
}
