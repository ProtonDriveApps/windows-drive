using Proton.Cryptography.Pgp;
using Proton.Drive.Sdk.Sync.Client.Contracts;

namespace Proton.Drive.Sdk.Sync.Client.RemoteNodes;

internal interface IExtendedAttributesReader
{
    Task<ExtendedAttributes?> ReadAsync(Link link, PgpPrivateKey nodeKey, CancellationToken cancellationToken);
}
