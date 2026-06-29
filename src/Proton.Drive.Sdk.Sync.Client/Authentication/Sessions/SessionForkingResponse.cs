using Proton.Drive.Shared.Client;

namespace Proton.Drive.Sdk.Sync.Client.Authentication.Sessions;

internal sealed record SessionForkingResponse : ApiResponse
{
    public string Selector { get; init; } = string.Empty;
}
