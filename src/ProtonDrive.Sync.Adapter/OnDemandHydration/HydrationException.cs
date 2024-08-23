using System;

namespace ProtonDrive.Sync.Adapter.OnDemandHydration;

public sealed class HydrationException : Exception
{
    public HydrationException(string message)
        : base(message)
    {
    }

    public HydrationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public HydrationException()
    {
    }
}
