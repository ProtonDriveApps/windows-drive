using System;

namespace ProtonDrive.Shared;

public interface IIdentifiable<out TId>
    where TId : IEquatable<TId>
{
    TId Id { get; }
}
