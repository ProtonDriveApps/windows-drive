namespace ProtonDrive.App.Account;

public enum UserQuotaStatus
{
    /// <summary>
    /// Quota usage is less then 80%
    /// </summary>
    Regular,

    /// <summary>
    /// Quota usage is between 80% and 90% (excluded)
    /// </summary>
    WarningLevel1Exceeded,

    /// <summary>
    /// Quota usage is between 90% and 100% (excluded)
    /// </summary>
    WarningLevel2Exceeded,

    /// <summary>
    /// Quota usage is 100% or more
    /// </summary>
    LimitExceeded,
}
