namespace Proton.Drive.Sdk.Sync.Client.Authentication;

public enum StartSessionResultCode
{
    /// <summary>
    /// Session successfully started
    /// </summary>
    Success,

    /// <summary>
    /// To start a session user sign-in is required
    /// </summary>
    SignInRequired,

    /// <summary>
    /// To finish multiple factor authentication a TOTP or FIDO2 verification is required
    /// </summary>
    SecondFactorRequired,

    /// <summary>
    /// To finish authentication a second (mailbox) password is required
    /// </summary>
    DataPasswordRequired,

    /// <summary>
    /// Session start failed
    /// </summary>
    Failure,
}
