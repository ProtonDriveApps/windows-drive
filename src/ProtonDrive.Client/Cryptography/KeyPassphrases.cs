using System;
using System.Collections.Generic;

namespace ProtonDrive.Client.Cryptography;

internal record KeyPassphrases(IReadOnlyDictionary<string, ReadOnlyMemory<byte>> Passphrases);
