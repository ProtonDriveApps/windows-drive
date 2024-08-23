using System;
using System.Collections.Generic;

namespace ProtonDrive.Sync.Engine.Shared;

public static class UpdateStatusExtensions
{
    public static bool Contains(this UpdateStatus value, UpdateStatus other)
    {
        if (other == UpdateStatus.Unchanged)
        {
            throw new ArgumentOutOfRangeException(nameof(other));
        }

        if (value == UpdateStatus.Unchanged)
        {
            return false;
        }

        return (value & other) == other;
    }

    public static UpdateStatus Intersect(this UpdateStatus value, UpdateStatus other)
    {
        return value & (other |
                        // Restore is not removed if Deleted or Created is not removed
                        ((other & (UpdateStatus.Deleted | UpdateStatus.Created)) != UpdateStatus.Unchanged ? UpdateStatus.Restore : UpdateStatus.Unchanged));
    }

    public static UpdateStatus Minus(this UpdateStatus value, UpdateStatus other)
    {
        return value & ~other &
               // Restore is removed when Deleted and Created are removed
               ((other & (UpdateStatus.Deleted | UpdateStatus.Created)) != UpdateStatus.Unchanged ? ~UpdateStatus.Restore : ~UpdateStatus.Unchanged);
    }

    public static UpdateStatus Union(this UpdateStatus value, UpdateStatus other)
    {
        if (value == other)
        {
            return value;
        }

        if (value == UpdateStatus.Unchanged)
        {
            return other;
        }

        if (other == UpdateStatus.Unchanged)
        {
            return value;
        }

        if (value.Contains(UpdateStatus.Created) && other.Contains(UpdateStatus.Deleted))
        {
            return UpdateStatus.Unchanged;
        }

        if (other.Contains(UpdateStatus.Deleted))
        {
            return other;
        }

        if (value.Contains(UpdateStatus.Created))
        {
            return value;
        }

        if (other.Contains(UpdateStatus.Created) || value.Contains(UpdateStatus.Deleted))
        {
            throw new InvalidOperationException();
        }

        return value | other;
    }

    public static IEnumerable<UpdateStatus> Split(this UpdateStatus value)
    {
        // Does not include UpdateStatus.Restore value
        var bits = (int)UpdateStatus.All;
        var flag = 1;

        while (bits != 0)
        {
            if ((bits & 1) != 0 && value.HasFlag((UpdateStatus)flag))
            {
                yield return (UpdateStatus)flag;
            }

            flag <<= 1;
            bits >>= 1;
        }
    }
}
