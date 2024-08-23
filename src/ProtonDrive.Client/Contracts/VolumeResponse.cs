namespace ProtonDrive.Client.Contracts;

public sealed record VolumeResponse : ApiResponse
{
    private readonly Volume? _volume;

    public Volume Volume
    {
        get => _volume ?? throw new ApiException(ResponseCode.InvalidValue, "Volume not available in API response");
        init => _volume = value;
    }
}
