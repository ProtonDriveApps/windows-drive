using System;

namespace ProtonDrive.Client.Contracts;

[Flags]
public enum MemberPermissions
{
    Write = 2,
    Read = 4,
}
