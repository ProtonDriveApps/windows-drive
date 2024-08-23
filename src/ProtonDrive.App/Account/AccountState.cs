namespace ProtonDrive.App.Account;

public sealed record AccountState
{
    internal AccountState(AccountStatus status, AccountErrorCode errorCode)
    {
        Status = status;
        ErrorCode = errorCode;
    }

    public AccountStatus Status { get; }
    public AccountErrorCode ErrorCode { get; }

    public static AccountState None { get; } = new(AccountStatus.None, AccountErrorCode.None);
}
