using Polly;

namespace Proton.Drive.Sdk.Sync.Client.Offline;

internal interface IOfflinePolicyProvider
{
    ResiliencePipeline<HttpResponseMessage> GetPolicy();
}
