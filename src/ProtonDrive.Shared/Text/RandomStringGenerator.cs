using System;

namespace ProtonDrive.Shared.Text;

public sealed class RandomStringGenerator
{
    private const string LowercaseLetters = "abcdefghijklmnopqrstuvwxyz";
    private const string UppercaseLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Numbers = "0123456789";

    private readonly string _characters;
    private readonly Random _random = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="RandomStringGenerator"/> class, using
    /// the specified character group.
    /// </summary>
    /// <param name="characterGroup">A set of character groups used to generate random
    /// strings.</param>
    public RandomStringGenerator(RandomStringCharacterGroup characterGroup)
    {
        Ensure.IsFalse(characterGroup == default, nameof(characterGroup));

        _characters = ((characterGroup & RandomStringCharacterGroup.LatinLowercase) != 0 ? LowercaseLetters : string.Empty)
                      + ((characterGroup & RandomStringCharacterGroup.LatinUppercase) != 0 ? UppercaseLetters : string.Empty)
                      + ((characterGroup & RandomStringCharacterGroup.Numbers) != 0 ? Numbers : string.Empty);
    }

    /// <summary>
    /// Generates random alphanumeric string.
    /// </summary>
    /// <param name="length">The length of the random string to generate.</param>
    /// <returns>The random alphanumeric string.</returns>
    public string GenerateRandomString(int length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        var randomChars = new char[length];

        for (var i = 0; i < randomChars.Length; i++)
        {
            randomChars[i] = _characters[_random.Next(_characters.Length)];
        }

        return new string(randomChars);
    }
}
