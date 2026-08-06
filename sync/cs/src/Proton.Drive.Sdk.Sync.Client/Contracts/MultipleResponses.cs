using System.Collections.Immutable;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public sealed record MultipleResponses<T> : ApiResponse
{
    private IImmutableList<T>? _responses;

    public IImmutableList<T> Responses
    {
        get => _responses ??= ImmutableList<T>.Empty;
        init => _responses = value;
    }
}
