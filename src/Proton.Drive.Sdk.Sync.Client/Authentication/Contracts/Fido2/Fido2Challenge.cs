using System.Text.Json;

namespace Proton.Drive.Sdk.Sync.Client.Authentication.Contracts.Fido2;

internal sealed class Fido2Challenge
{
    public JsonElement AuthenticationOptions { get; set; }

    public IReadOnlyList<Fido2RegisteredKey> RegisteredKeys { get; set; } = [];
}
