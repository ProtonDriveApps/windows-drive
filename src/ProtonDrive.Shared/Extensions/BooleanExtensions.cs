namespace ProtonDrive.Shared.Extensions;

public static class BooleanExtensions
{
    public static bool Succeeded(this bool result) => result;

    public static bool Failed(this bool result) => !result;
}
