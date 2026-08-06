using Proton.Drive.Sdk.Sync.Client.Contracts;

namespace Proton.Drive.Sdk.Sync.Client.Cryptography;

public sealed record Address(string Id, string EmailAddress, AddressStatus Status, IReadOnlyList<AddressKey> Keys, int PrimaryKeyIndex)
{
    public AddressKey GetPrimaryKey() => Keys[PrimaryKeyIndex];
}
