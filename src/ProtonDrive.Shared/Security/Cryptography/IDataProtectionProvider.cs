using System;

namespace ProtonDrive.Shared.Security.Cryptography;

public interface IDataProtectionProvider
{
    string Protect(string data);
    ReadOnlyMemory<byte> Protect(ReadOnlyMemory<byte> data);
    string Unprotect(string data);
    ReadOnlyMemory<byte> Unprotect(ReadOnlyMemory<byte> data);
}
