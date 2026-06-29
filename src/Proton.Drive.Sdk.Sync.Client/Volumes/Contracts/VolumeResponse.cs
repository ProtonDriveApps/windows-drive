using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Volumes.Contracts;

public sealed record VolumeResponse : ApiResponse
{
    private readonly Volume? _volume;

    public Volume Volume
    {
        get => _volume ?? throw new ApiException(ResponseCode.InvalidValue, "Volume not available in API response");
        init => _volume = value;
    }
}
