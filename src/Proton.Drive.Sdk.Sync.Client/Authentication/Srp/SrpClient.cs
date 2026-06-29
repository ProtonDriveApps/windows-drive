namespace Proton.Drive.Sdk.Sync.Client.Authentication.Srp;

internal sealed class SrpClient(Proton.Cryptography.Srp.SrpClient srpClient) : ISrpClient
{
    private readonly Proton.Cryptography.Srp.SrpClient _srpClient = srpClient;

    public ISrpClientHandshake ComputeHandshake(ReadOnlySpan<byte> serverEphemeral, int bitLength)
    {
        return new SrpClientHandshake(_srpClient.ComputeHandshake(serverEphemeral, bitLength));
    }
}
