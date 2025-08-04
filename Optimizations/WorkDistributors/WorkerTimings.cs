using System;
using System.Linq;
using Weaver.Extensions;

namespace Weaver.Optimizations.WorkDistributors;

internal sealed class WorkerTimings
{
    private readonly HighStopwatch _stopWatch = new();
    public double[] WorkTypeTimings { get; }

    public WorkerTimings()
    {
        WorkTypeTimings = new double[ArrayExtensions.GetEnumValuesEnumerable<WorkType>().Max(x => (int)x) + 1];
    }

    public void StartTimer()
    {
        _stopWatch.Begin();
    }

    public void RecordTime(WorkType workType)
    {
        WorkTypeTimings[(int)workType] += _stopWatch.duration;
    }

    public void Reset()
    {
        Array.Clear(WorkTypeTimings, 0, WorkTypeTimings.Length);
    }
}
