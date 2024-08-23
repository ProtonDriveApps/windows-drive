using System.Collections.Immutable;

namespace ProtonDrive.Client.Contracts;

public sealed record VolumeListResponse : ApiResponse
{
    private IImmutableList<Volume>? _volumes;

    public IImmutableList<Volume> Volumes
    {
        get => _volumes ??= ImmutableList<Volume>.Empty;
        init => _volumes = value;
    }
}
