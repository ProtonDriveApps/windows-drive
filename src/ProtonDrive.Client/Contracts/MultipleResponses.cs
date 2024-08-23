using System.Collections.Immutable;

namespace ProtonDrive.Client.Contracts;

public sealed record MultipleResponses<T> : ApiResponse
{
    private IImmutableList<T>? _responses;

    public IImmutableList<T> Responses
    {
        get => _responses ??= ImmutableList<T>.Empty;
        init => _responses = value;
    }
}
