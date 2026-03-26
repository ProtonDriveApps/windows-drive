namespace ProtonDrive.Client.Features.Contracts;

internal sealed record CoreFeatureListResponse : ApiResponse
{
    private IReadOnlyCollection<CoreFeature>? _features;

    public IReadOnlyCollection<CoreFeature> Features
    {
        get => _features ??= [];
        init => _features = value;
    }
}
