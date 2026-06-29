using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public sealed record OrganizationResponse : ApiResponse
{
    private readonly Organization? _organization;

    public Organization Organization
    {
        get => _organization ?? throw new ApiException("Organization is not set");
        init => _organization = value;
    }
}
