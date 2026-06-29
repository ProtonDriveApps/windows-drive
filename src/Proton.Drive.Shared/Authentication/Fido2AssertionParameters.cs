using System.Text.Json;

namespace Proton.Drive.Shared.Authentication;

public sealed class Fido2AssertionParameters
{
    public JsonElement AuthenticationOptions { get; init; }
}
