using Proton.Drive.Sdk.Sync.Client.Settings.Contracts;
using Refit;

namespace Proton.Drive.Sdk.Sync.Client.Settings;

public interface ISettingsApiClient
{
    [Get("/v4/settings")]
    [BearerAuthorizationHeader]
    Task<SettingsResponse> GetAsync(CancellationToken cancellationToken);
}
