namespace Proton.Drive.Sdk.Sync.Client.Authentication.Srp;

internal sealed class SrpClientHandshake(Proton.Cryptography.Srp.SrpClientHandshake clientHandshake) : ISrpClientHandshake
{
    private readonly Proton.Cryptography.Srp.SrpClientHandshake _clientHandshake = clientHandshake;

    public byte[] Ephemeral => _clientHandshake.Ephemeral;
    public byte[] Proof => _clientHandshake.Proof;

    public bool TryComputeSharedKey(ReadOnlySpan<byte> serverProof)
    {
        return _clientHandshake.TryComputeSharedKey(serverProof);
    }
}
