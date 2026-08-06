using Proton.Drive.Sdk.Sync.Client.Devices.Contracts;
using Proton.Drive.Shared.Client;
using Refit;

namespace Proton.Drive.Sdk.Sync.Client.Devices;

internal interface IDeviceApiClient
{
    [Get("/devices")]
    [BearerAuthorizationHeader]
    Task<DeviceListResponse> GetAllAsync(CancellationToken cancellationToken);

    [Post("/devices")]
    [BearerAuthorizationHeader]
    Task<DeviceCreationResponse> CreateAsync(DeviceCreationParameters parameters, CancellationToken cancellationToken);

    [Put("/devices/{id}")]
    [BearerAuthorizationHeader]
    Task<ApiResponse> UpdateAsync(string id, DeviceUpdateParameters parameters, CancellationToken cancellationToken);

    [Delete("/devices/{id}")]
    [BearerAuthorizationHeader]
    Task<ApiResponse> DeleteAsync(string id, CancellationToken cancellationToken);
}
