namespace ProtonDrive.App.Authentication;

public enum SessionStatus
{
    /// <summary>
    /// User session not started, initial status
    /// </summary>
    NotStarted,

    /// <summary>
    /// Starting the user session including the saved session
    /// </summary>
    Starting,

    /// <summary>
    /// Interactively signing in
    /// </summary>
    SigningIn,

    /// <summary>
    /// User session has been successfully started
    /// </summary>
    Started,

    /// <summary>
    /// User session or an attempt to start user session is ending
    /// </summary>
    Ending,

    /// <summary>
    /// Failed to start saved user session
    /// </summary>
    Failed,
}
