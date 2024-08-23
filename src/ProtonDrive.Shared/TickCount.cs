using System;

namespace ProtonDrive.Shared;

public readonly struct TickCount : IEquatable<TickCount>, IComparable<TickCount>
{
    private readonly long _value;

    private TickCount(long value)
    {
        _value = value;
    }

    public static TickCount Current => new(Environment.TickCount64);
    public static TickCount MinValue { get; } = new(long.MinValue);
    public static TickCount MaxValue { get; } = new(long.MaxValue);

    public static bool operator ==(TickCount left, TickCount right) => left.Equals(right);
    public static bool operator !=(TickCount left, TickCount right) => !(left == right);
    public static bool operator >(TickCount left, TickCount right) => left._value > right._value;
    public static bool operator <(TickCount left, TickCount right) => left._value < right._value;
    public static bool operator >=(TickCount left, TickCount right) => left._value >= right._value;
    public static bool operator <=(TickCount left, TickCount right) => left._value <= right._value;

    public static TimeSpan operator -(TickCount value, TickCount other) => TimeSpan.FromTicks((value._value - other._value) * TimeSpan.TicksPerMillisecond);
    public static TickCount operator -(TickCount value, TimeSpan other) => new(value._value - (other.Ticks / TimeSpan.TicksPerMillisecond));
    public static TickCount operator +(TickCount value, TimeSpan other) => new(value._value + (other.Ticks / TimeSpan.TicksPerMillisecond));

    public int CompareTo(TickCount other) => _value.CompareTo(other._value);

    public override bool Equals(object? obj) => obj is TickCount other && Equals(other);

    public bool Equals(TickCount other) => _value == other._value;

    public override int GetHashCode() => _value.GetHashCode();
}
