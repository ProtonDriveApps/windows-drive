using System.Threading;
using System.Threading.Tasks;
using ProtonDrive.Client.Settings.Contracts;
using Refit;

namespace ProtonDrive.Client.Settings;

public interface ISettingsApiClient
{
    [Get("/v4/settings")]
    [BearerAuthorizationHeader]
    Task<SettingsResponse> GetAsync(CancellationToken cancellationToken);
}
