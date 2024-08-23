using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Contracts;
using Refit;

namespace ProtonDrive.Client;

public interface IPaymentsApiClient
{
    [Get("/v4/plans/default")]
    Task<DefaultPlanResponse> GetDefaultPlanAsync(CancellationToken cancellationToken);

    [Get("/v4/subscription")]
    [BearerAuthorizationHeader]
    Task<SubscriptionResponse> GetSubscriptionAsync(CancellationToken cancellationToken);
}
