using System.Collections.Generic;
using System.Threading;

namespace Weaver.Optimizations.LinearDataAccess.WorkDistributors;

internal sealed class StarClusterWorkManager
{
    private PlanetWorkManager[] _planetWorkManagers;
    private PlanetWorkManager[] _allPlanetWorkManagers;
    private int _planetsWithWorkScheduledCount;
    private int _planetsNotCompletedCount;

    public int Parallelism { get; private set; }

    public StarClusterWorkManager(PlanetFactory[] allPlanets, int parallelism)
    {
        _planetWorkManagers = new PlanetWorkManager[allPlanets.Length];
        List<PlanetWorkManager> allPlanetWorkManagers = [];
        Parallelism = parallelism;

        for (int i = 0; i < allPlanets.Length; i++)
        {
            if (allPlanets[i] == null)
            {
                continue;
            }

            OptimizedPlanet optimizedPlanet = OptimizedStarCluster.GetOptimizedPlanet(allPlanets[i]);
            allPlanetWorkManagers.Add(new PlanetWorkManager(allPlanets[i], optimizedPlanet, parallelism));
        }

        _planetWorkManagers = allPlanetWorkManagers.ToArray();
        _allPlanetWorkManagers = allPlanetWorkManagers.ToArray();

        Reset();
    }

    public void SetMaxWorkParallelism(int parallelism)
    {
        Parallelism = parallelism;

        for (int i = 0; i < _planetWorkManagers.Length; i++)
        {
            _planetWorkManagers[i].SetMaxWorkParallelism(parallelism);
        }
    }

    public PlanetWorkPlan? TryGetWork()
    {
        int planetsNotCompletedCount = _planetsNotCompletedCount;
        while (_planetsWithWorkScheduledCount < planetsNotCompletedCount)
        {
            int planetIndex = Interlocked.Increment(ref _planetsWithWorkScheduledCount) - 1;
            if (planetIndex >= planetsNotCompletedCount)
            {
                break;
            }

            PlanetWorkPlan? planetWorkPlan = TryGetWork(planetIndex);
            if (planetWorkPlan != null)
            {
                return planetWorkPlan;
            }
        }

        //while (true)
        //{
        bool hasIncompleteWork = false;
        for (int i = 0; i < planetsNotCompletedCount; i++)
        {
            if (_planetWorkManagers[i] == null)
            {
                continue;
            }
            hasIncompleteWork = true;

            PlanetWorkPlan? planetWorkPlan = TryGetWork(i);
            if (planetWorkPlan != null)
            {
                return planetWorkPlan;
            }
        }

        //if (!hasIncompleteWork)
        //{
        //    break;
        //}

        for (int i = 0; i < planetsNotCompletedCount; i++)
        {
            PlanetWorkPlan? planetWorkPlan = TryWaitForWork(i);
            if (planetWorkPlan != null)
            {
                return planetWorkPlan;
            }
        }
        //}

        return null;
    }

    public void Reset()
    {
        _allPlanetWorkManagers.CopyTo(_planetWorkManagers, 0);
        for (int i = 0; i < _planetWorkManagers.Length; i++)
        {
            _planetWorkManagers[i].Reset();
        }

        _planetsWithWorkScheduledCount = 0;
        _planetsNotCompletedCount = _planetWorkManagers.Length;
    }

    private PlanetWorkPlan? TryGetWork(int planetIndex)
    {
        PlanetWorkManager planetWorkManager = _planetWorkManagers[planetIndex];
        if (planetWorkManager == null)
        {
            return null;
        }

        WorkPlan? workPlan = planetWorkManager.TryGetWork(out bool canScheduleMoreWork);
        if (!canScheduleMoreWork)
        {
            int lastPlanetNotCompletedIndex = Interlocked.Decrement(ref _planetsNotCompletedCount);
            if (lastPlanetNotCompletedIndex > 0)
            {
                _planetWorkManagers[planetIndex] = _planetWorkManagers[lastPlanetNotCompletedIndex];
            }
        }
        if (workPlan == null)
        {
            return null;
        }

        return new PlanetWorkPlan(planetWorkManager, workPlan.Value);
    }

    private PlanetWorkPlan? TryWaitForWork(int planetIndex)
    {
        PlanetWorkManager planetWorkManager = _planetWorkManagers[planetIndex];
        if (planetWorkManager == null)
        {
            return null;
        }

        WorkPlan? workPlan = planetWorkManager.TryWaitForWork();
        if (workPlan == null)
        {
            return null;
        }

        return new PlanetWorkPlan(planetWorkManager, workPlan.Value);
    }
}
