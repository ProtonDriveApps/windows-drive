namespace ProtonDrive.Client.Authentication;

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
    /// To finish two factor authentication a second factor code is required
    /// </summary>
    SecondFactorCodeRequired,

    /// <summary>
    /// To finish authentication a second (mailbox) password is required
    /// </summary>
    DataPasswordRequired,

    /// <summary>
    /// Session start failed
    /// </summary>
    Failure,
}
