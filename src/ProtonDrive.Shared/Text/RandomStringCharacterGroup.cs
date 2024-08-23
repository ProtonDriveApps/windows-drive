using System;

namespace ProtonDrive.Shared.Text;

[Flags]
public enum RandomStringCharacterGroup
{
    Numbers = 1,
    LatinUppercase = 2,
    LatinLowercase = 4,

    NumbersAndLatinUppercase = Numbers | LatinUppercase,
    NumbersAndLatinLowercase = Numbers | LatinLowercase,
    NumberAndLatin = Numbers | LatinLowercase | LatinUppercase,
    Latin = LatinUppercase | LatinLowercase,
}
