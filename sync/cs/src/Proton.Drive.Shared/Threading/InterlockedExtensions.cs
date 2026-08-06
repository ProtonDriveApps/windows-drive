namespace Proton.Drive.Shared.Threading;

public static class InterlockedExtensions
{
    public static T Update<T>(ref T location, Func<T, T> updateValueFactory)
        where T : class
    {
        T originalValue = location;
        T updatedValue;

        do
        {
            updatedValue = updateValueFactory.Invoke(originalValue);
        }
        while (originalValue != (originalValue = Interlocked.CompareExchange(ref location, updatedValue, originalValue)));

        return updatedValue;
    }
}
