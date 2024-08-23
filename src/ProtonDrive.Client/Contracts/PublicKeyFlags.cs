using System;

namespace ProtonDrive.Client.Contracts;

[Flags]
public enum PublicKeyFlags
{
    IsNotCompromised = 1,
    IsNotObsolete = 2,
}
