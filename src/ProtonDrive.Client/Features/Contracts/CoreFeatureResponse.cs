namespace ProtonDrive.Client.Features.Contracts;

internal sealed record CoreFeatureResponse : ApiResponse
{
    private readonly CoreFeature? _feature;

    public CoreFeature Feature
    {
        get => _feature ?? throw new ApiException("Feature is not set");
        init => _feature = value;
    }
}
