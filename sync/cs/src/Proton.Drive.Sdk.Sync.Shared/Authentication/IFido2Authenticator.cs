using Proton.Drive.Shared.Authentication;

namespace Proton.Drive.Sdk.Sync.Shared.Authentication;

public interface IFido2Authenticator
{
    bool IsAvailable { get; }

    Task<Fido2AssertionResult> AssertAsync(Fido2AssertionParameters parameters, CancellationToken cancellationToken);
}
