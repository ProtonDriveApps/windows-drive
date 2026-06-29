using Proton.Drive.Shared.Authentication;

namespace Proton.Drive.Sdk.Sync.Client.Authentication;

public sealed class MultiFactorAuthenticationParameters
{
    public MultiFactorAuthenticationMethods Methods { get; init; }

    public Fido2AssertionParameters? Fido2 { get; init; }
}
