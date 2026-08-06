using Proton.Drive.Sdk.Sync.Client.Authentication.Contracts.Fido2;
using Proton.Drive.Shared.Authentication;

namespace Proton.Drive.Sdk.Sync.Client.Authentication;

internal static class TwoFactorExtensions
{
    public static MultiFactorAuthenticationParameters GetMultiFactorParameters(this Contracts.MultiFactorAuthenticationParameters multiFactor)
    {
        return new MultiFactorAuthenticationParameters
        {
            Methods = multiFactor.Methods,
            Fido2 = GetAssertionParameters(multiFactor.Fido2Challenge),
        };
    }

    private static Fido2AssertionParameters? GetAssertionParameters(Fido2Challenge? fido2Challenge)
    {
        if (fido2Challenge?.AuthenticationOptions is null)
        {
            return null;
        }

        return new Fido2AssertionParameters
        {
            AuthenticationOptions = fido2Challenge.AuthenticationOptions,
        };
    }
}
