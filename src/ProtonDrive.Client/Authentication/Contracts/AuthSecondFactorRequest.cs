namespace ProtonDrive.Client.Authentication.Contracts;

public sealed record AuthSecondFactorRequest
{
    public string TwoFactorCode { get; init; } = string.Empty;
}
