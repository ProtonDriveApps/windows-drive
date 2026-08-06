using Proton.Drive.Native.Authentication;
using Proton.Drive.Sdk.Sync.Shared.Authentication;
using Proton.Drive.Shared.Authentication;

namespace Proton.Drive.App.Windows.Authentication;

internal class Win32Fido2Authenticator : IFido2Authenticator
{
    public bool IsAvailable => WebAuthN.IsAvailable;

    public Task<Fido2AssertionResult> AssertAsync(Fido2AssertionParameters parameters, CancellationToken cancellationToken)
    {
        return WebAuthN.GetAssertionResponseAsync(parameters, cancellationToken: cancellationToken);
    }
}
