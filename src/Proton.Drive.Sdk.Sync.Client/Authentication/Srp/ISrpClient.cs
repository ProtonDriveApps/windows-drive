namespace Proton.Drive.Sdk.Sync.Client.Authentication.Srp;

internal interface ISrpClient
{
    ISrpClientHandshake ComputeHandshake(ReadOnlySpan<byte> serverEphemeral, int bitLength);
}
