using Polly;

namespace ProtonDrive.Client.Offline;

internal interface IOfflinePolicyProvider
{
    ResiliencePipeline<HttpResponseMessage> GetPolicy();
}
