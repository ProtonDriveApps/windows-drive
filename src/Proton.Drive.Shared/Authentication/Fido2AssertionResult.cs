using System.Text.Json;

namespace Proton.Drive.Shared.Authentication;

public sealed class Fido2AssertionResult
{
    public required JsonElement AuthenticationOptions { get; init; }
    public required byte[] ClientData { get; init; }
    public required byte[] AuthenticatorData { get; init; }
    public required byte[] Signature { get; init; }
    public required byte[] CredentialId { get; init; }
}
