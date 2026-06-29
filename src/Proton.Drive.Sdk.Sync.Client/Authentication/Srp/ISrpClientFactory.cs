using System.Net;
using Proton.Drive.Sdk.Sync.Client.Authentication.Contracts;

namespace Proton.Drive.Sdk.Sync.Client.Authentication.Srp;

internal interface ISrpClientFactory
{
    ISrpClient Create(NetworkCredential credential, AuthInfo authInfo);
}
