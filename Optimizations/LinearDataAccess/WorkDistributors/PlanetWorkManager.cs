using System;
using System.Threading;

namespace Weaver.Optimizations.LinearDataAccess.WorkDistributors;

internal sealed class PlanetWorkManager
{
    private WorkTracker[] _workTrackers;
    private int _currentWorkTrackerIndex;

    public PlanetFactory Planet { get; }
    public OptimizedPlanet OptimizedPlanet { get; }

    public PlanetWorkManager(PlanetFactory planet, OptimizedPlanet optimizedPlanet, int parallelism)
    {
        _workTrackers = optimizedPlanet.GetMultithreadedWork(parallelism);

        Planet = planet;
        OptimizedPlanet = optimizedPlanet;
    }

    public void SetMaxWorkParallelism(int parallelism)
    {
        for (int i = 0; i < _workTrackers.Length; i++)
        {
            _workTrackers[i].Dispose();
        }

        OptimizedPlanet optimizedPlanet = OptimizedStarCluster.GetOptimizedPlanet(Planet);
        _workTrackers = optimizedPlanet.GetMultithreadedWork(parallelism);
    }

    public WorkPlan? TryGetWork(out bool canScheduleMoreWork)
    {
        int currentWorkTrackerIndex = _currentWorkTrackerIndex;
        if (currentWorkTrackerIndex == _workTrackers.Length)
        {
            canScheduleMoreWork = false;
            return null;
        }

        WorkTracker workTracker = _workTrackers[currentWorkTrackerIndex];
        if (workTracker.ScheduledCount >= workTracker.MaxWorkCount)
        {
            canScheduleMoreWork = currentWorkTrackerIndex + 1 < _workTrackers.Length;
            return null;
        }

        int workIndex = Interlocked.Increment(ref _workTrackers[currentWorkTrackerIndex].ScheduledCount) - 1;
        if (workIndex >= workTracker.MaxWorkCount)
        {
            canScheduleMoreWork = currentWorkTrackerIndex + 1 < _workTrackers.Length;
            return null;
        }

        canScheduleMoreWork = true;
        return new WorkPlan(workTracker.WorkType, currentWorkTrackerIndex, workIndex, workTracker.MaxWorkCount);
    }

    public WorkPlan? TryWaitForWork()
    {
        int currentWorkTrackerIndex = _currentWorkTrackerIndex;
        if (currentWorkTrackerIndex == _workTrackers.Length)
        {
            return null;
        }

        int nextWorkTrackerIndex = currentWorkTrackerIndex + 1;
        if (nextWorkTrackerIndex >= _workTrackers.Length)
        {
            return null;
        }

        WorkTracker workTracker = _workTrackers[nextWorkTrackerIndex];
        if (workTracker.ScheduledCount >= workTracker.MaxWorkCount)
        {
            return null;
        }

        int workIndex = Interlocked.Increment(ref _workTrackers[nextWorkTrackerIndex].ScheduledCount) - 1;
        if (workIndex >= workTracker.MaxWorkCount)
        {
            return null;
        }

        _workTrackers[currentWorkTrackerIndex].WaitForCompletion.Wait();
        return new WorkPlan(workTracker.WorkType, nextWorkTrackerIndex, workIndex, workTracker.MaxWorkCount);
    }

    public void CompleteWork(WorkPlan workPlan)
    {
        ref WorkTracker workTracker = ref _workTrackers[workPlan.WorkTrackerIndex];
        int currentCount = Interlocked.Increment(ref workTracker.CompletedCount);
        if (currentCount == workTracker.MaxWorkCount)
        {
            Interlocked.Increment(ref _currentWorkTrackerIndex);
            workTracker.WaitForCompletion.Set();
        }
        else if (currentCount > workTracker.MaxWorkCount)
        {
            throw new InvalidOperationException($"Completed more work for {workPlan.WorkType} than the max {workTracker.MaxWorkCount}");
        }
    }

    public void Reset()
    {
        for (int i = 0; i < _workTrackers.Length; i++)
        {
            _workTrackers[i].Reset();
        }
        _currentWorkTrackerIndex = 0;
    }
}
