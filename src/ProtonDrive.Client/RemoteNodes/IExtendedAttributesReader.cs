using System.Threading;
using System.Threading.Tasks;
using Proton.Security.Cryptography.Abstractions;
using ProtonDrive.Client.Contracts;

namespace ProtonDrive.Client.RemoteNodes;

internal interface IExtendedAttributesReader
{
    Task<ExtendedAttributes?> ReadAsync(Link link, PrivatePgpKey nodeKey, CancellationToken cancellationToken);
}
