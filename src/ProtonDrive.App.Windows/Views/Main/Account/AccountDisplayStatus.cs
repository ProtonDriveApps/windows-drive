namespace ProtonDrive.App.Windows.Views.Main.Account;

/// <summary>
/// User account status that is created by combining Session status and Account status
/// </summary>
public enum AccountDisplayStatus
{
    /// <summary>
    /// User session has not been started
    /// </summary>
    SignedOut,

    /// <summary>
    /// The user session is starting
    /// </summary>
    SigningIn,

    /// <summary>
    /// The user session has been started, the account is being validated
    /// </summary>
    SettingUp,

    /// <summary>
    /// The account passed the validation
    /// </summary>
    Succeeded,

    /// <summary>
    /// The account didn't pass the validation or an error occurred during
    /// the validation (access to Proton Drive API has failed)
    /// </summary>
    AccountError,

    /// <summary>
    /// The user session is ending
    /// </summary>
    SigningOut,

    /// <summary>
    /// Failed to start the user session
    /// </summary>
    SessionError,
}
