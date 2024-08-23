using System.Net.Http;
using Polly;

namespace ProtonDrive.Client.Offline;

internal interface IOfflinePolicyProvider
{
    AsyncPolicy<HttpResponseMessage> GetPolicy();
}
