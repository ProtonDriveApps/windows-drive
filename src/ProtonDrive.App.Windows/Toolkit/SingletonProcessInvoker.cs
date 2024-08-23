using System;
using System.Threading;

namespace ProtonDrive.App.Windows.Toolkit;

public static class SingletonProcessInvoker
{
    private const string UniqueMutexName = "Proton Drive - {231bb06d-d72b-44b9-847b-8ac1c7691ecd}";

    public static bool TryInvoke(Action action)
    {
        using var mutex = new Mutex(true, UniqueMutexName, out var newMutexCreated);

        if (!newMutexCreated)
        {
            return false;
        }

        action.Invoke();

        return true;
    }
}
