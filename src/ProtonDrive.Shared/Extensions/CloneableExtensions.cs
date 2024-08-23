using System;

namespace ProtonDrive.Shared.Extensions;

public static class CloneableExtensions
{
    public static T Copy<T>(this T obj)
        where T : class, ICloneable
    {
        return (T)obj.Clone();
    }
}
