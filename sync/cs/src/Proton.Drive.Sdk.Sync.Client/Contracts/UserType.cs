namespace Proton.Drive.Sdk.Sync.Client.Contracts;

public enum UserType
{
    Proton = 1, // internal
    Managed = 2, // sub-user
    External = 3,
    Credentialless = 4,
}
