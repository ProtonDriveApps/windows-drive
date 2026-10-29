namespace Proton.Drive.Sdk.Sync.Client.Authentication.Contracts;

internal sealed record RefreshSessionResponse : UnauthenticatedSessionResponse
{
    public int RefreshCounter { get; init; }
}
