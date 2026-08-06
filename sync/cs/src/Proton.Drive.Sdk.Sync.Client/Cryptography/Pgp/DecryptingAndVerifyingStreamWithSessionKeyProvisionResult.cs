using Proton.Cryptography.Pgp;

namespace Proton.Drive.Sdk.Sync.Client.Cryptography.Pgp;

public record DecryptingAndVerifyingStreamWithSessionKeyProvisionResult(
    Stream DecryptionStream,
    PgpSessionKey SessionKey,
    Func<PgpVerificationStatus> GetVerificationStatus);
