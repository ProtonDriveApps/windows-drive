﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See LICENSE-MIT file in the project root for full license information.
//
// Adapted from https://github.com/dotnet/extensions/blob/master/src/Shared/src/ValueStopwatch/ValueStopwatch.cs

using System;
using System.Diagnostics;

namespace ProtonDrive.Shared;

internal readonly struct ValueStopwatch
{
    private static readonly double TimestampToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;

    private readonly long _startTimestamp;

    private ValueStopwatch(long startTimestamp)
    {
        _startTimestamp = startTimestamp;
    }

    public bool IsActive => _startTimestamp != 0;

    public static ValueStopwatch StartNew() => new(Stopwatch.GetTimestamp());

    public TimeSpan GetElapsedTime()
    {
        // Start timestamp can't be zero in an initialized ValueStopwatch. It would have to be literally the first thing executed when the machine boots to be 0.
        // So it being 0 is a clear indication of default(ValueStopwatch)
        if (!IsActive)
        {
            throw new InvalidOperationException("An uninitialized, or 'default', ValueStopwatch cannot be used to get elapsed time.");
        }

        var end = Stopwatch.GetTimestamp();
        var timestampDelta = end - _startTimestamp;
        var ticks = (long)(TimestampToTicks * timestampDelta);
        return new TimeSpan(ticks);
    }
}
