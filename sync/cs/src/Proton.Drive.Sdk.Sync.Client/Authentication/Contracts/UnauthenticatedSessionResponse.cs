using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Authentication.Contracts;

internal record UnauthenticatedSessionResponse : ApiResponse
{
    [JsonPropertyName("UID")]
    public string SessionId
    {
        get => field ?? throw new ApiException($"{nameof(SessionId)} is not set");
        init;
    }

    public string AccessToken
    {
        get => field ?? throw new ApiException($"{nameof(AccessToken)} is not set");
        init;
    }

    public string RefreshToken
    {
        get => field ?? throw new ApiException($"{nameof(RefreshToken)} is not set");
        init;
    }

    public IImmutableList<string> Scopes { get; init; } = ImmutableList<string>.Empty;
}
