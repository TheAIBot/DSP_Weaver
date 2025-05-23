using System;
using System.Threading;

namespace Weaver.Optimizations.LinearDataAccess.WorkDistributors;

internal struct WorkTracker : IDisposable
{
    public readonly WorkType WorkType;
    public int ScheduledCount;
    public int CompletedCount;
    public int MaxWorkCount;
    public readonly ManualResetEventSlim WaitForCompletion;

    public WorkTracker(WorkType workType, int maxWorkCount)
    {
        WorkType = workType;
        ScheduledCount = 0;
        CompletedCount = 0;
        MaxWorkCount = maxWorkCount;
        WaitForCompletion = new(false);
    }

    public void Reset()
    {
        ScheduledCount = 0;
        CompletedCount = 0;
        WaitForCompletion.Reset();
    }

    public readonly void Dispose()
    {
        WaitForCompletion.Dispose();
    }
}
