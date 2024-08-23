namespace ProtonDrive.App.Account;

public enum AccountErrorCode
{
    None,

    /// <summary>
    /// The user is not eligible to access Proton Drive
    /// </summary>
    NoDriveAccess,

    /// <summary>
    /// The user has unpaid invoices
    /// </summary>
    Delinquent,

    /// <summary>
    /// Accessing Proton Drive API has failed
    /// </summary>
    DriveAccessFailed,

    /// <summary>
    /// Failed to switch user account
    /// </summary>
    AccountSwitchingFailed,
}
