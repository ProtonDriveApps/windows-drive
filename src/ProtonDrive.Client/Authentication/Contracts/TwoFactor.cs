namespace ProtonDrive.Client.Authentication.Contracts;

public sealed record TwoFactor
{
    public int Enabled { get; init; }
}
