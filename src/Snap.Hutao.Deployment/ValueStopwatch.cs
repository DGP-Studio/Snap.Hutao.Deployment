using System;
using System.Diagnostics;

namespace Snap.Hutao.Deployment;

internal readonly struct ValueStopwatch
{
    private readonly long startTimestamp;

    private ValueStopwatch(long startTimestamp)
    {
        this.startTimestamp = startTimestamp;
    }

    public bool IsActive
    {
        get => startTimestamp != 0;
    }

    public static ValueStopwatch StartNew()
    {
        return new(Stopwatch.GetTimestamp());
    }

    public TimeSpan GetElapsedTime()
    {
        return Stopwatch.GetElapsedTime(startTimestamp);
    }
}