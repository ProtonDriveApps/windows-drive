using System.Collections.Immutable;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Volumes.Contracts;

public sealed record VolumeListResponse : ApiResponse
{
    private IImmutableList<Volume>? _volumes;

    public IImmutableList<Volume> Volumes
    {
        get => _volumes ??= ImmutableList<Volume>.Empty;
        init => _volumes = value;
    }
}
