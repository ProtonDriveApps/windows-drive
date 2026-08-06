using System.Security;
using Proton.Cryptography.Pgp;

namespace Proton.Drive.Sdk.Sync.Client.Cryptography.Pgp;

public interface IPgpKeyPacketProducer
{
    ReadOnlyMemory<byte> GetKeyPacket(PgpPublicKey publicKey);
    ReadOnlyMemory<byte> GetKeyPacket(SecureString password);
}
