using System.Diagnostics.CodeAnalysis;

namespace ProtonDrive.Shared;

public static class ValueExtensions
{
    public static bool TryUpdate<T>([NotNullIfNotNull(nameof(newValue))] ref T field, T newValue)
    {
        if (EqualityComparer<T>.Default.Equals(field, newValue))
        {
            return false;
        }

        field = newValue;

        return true;
    }
}
