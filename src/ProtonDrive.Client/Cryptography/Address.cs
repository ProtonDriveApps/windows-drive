using System.Collections.Generic;
using ProtonDrive.Client.Contracts;

namespace ProtonDrive.Client.Cryptography;

public sealed record Address(string Id, string EmailAddress, AddressStatus Status, IReadOnlyList<AddressKey> Keys, int PrimaryKeyIndex)
{
    public AddressKey GetPrimaryKey() => Keys[PrimaryKeyIndex];
}
